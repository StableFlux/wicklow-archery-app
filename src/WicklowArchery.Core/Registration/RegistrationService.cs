using System.Net.Http;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;

namespace WicklowArchery.Core.Registration;

public sealed record FormLoadResult(FormCheck? Check, string? Error);

public enum PersonResultState { Pending, Sending, Submitted, Failed, Skipped }

public sealed record PersonResult(ConfirmItem Item, PersonResultState State, string? Error, DateTimeOffset At);

public sealed class RegistrationService(IFormClient client, TimeProvider clock)
{
    public const string NetworkErrorMessage = "Couldn't reach the form. Check your internet connection.";
    public const string NotFoundMessage = "That form couldn't be found. Check the form address in Settings.";
    public const string SignInMessage = "That form needs a Google sign-in, which this app doesn't support.";
    public const string ClosedMessage = "The form isn't accepting responses right now.";
    public const string TimeoutMessage = "Timed out. It may have been recorded, so check the form's responses before sending again.";

    public async Task<FormLoadResult> LoadFormAsync(string formUrl, CancellationToken ct)
    {
        const string parseError = "The form page couldn't be read. Google may have changed its format.";
        var formUri = FormAddress.TryParse(formUrl);
        if (formUri is null)
            return new FormLoadResult(null, "That form address isn't a Google Form link. Use the full docs.google.com/forms link.");
        string? html = null;
        try
        {
            html = await client.GetAsync(formUri, ct).ConfigureAwait(false);
            var check = FormMapper.Map(FormParser.Parse(html));
            if (check.Form is { } form
                && (!FormAddress.IsAllowed(form.ResponseUrl)
                    || FormAddress.FormId(form.ResponseUrl) != FormAddress.FormId(formUri)))
                return new FormLoadResult(null, parseError);
            return new FormLoadResult(check, null);
        }
        catch (HttpRequestException ex)
        {
            // No status means no answer at all: a network problem. An answer with a status is explained.
            return new FormLoadResult(null, ex.StatusCode is null ? NetworkErrorMessage : StatusMessage(ex));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FormLoadResult(null, NetworkErrorMessage);
        }
        catch (FormParseException)
        {
            return new FormLoadResult(null, IsClosedPage(html) ? ClosedMessage : parseError);
        }
    }

    private static string StatusMessage(HttpRequestException ex)
    {
        var status = (int)ex.StatusCode!.Value;
        var location = (ex as FormHttpStatusException)?.Location?.ToString() ?? "";
        return status switch
        {
            404 => NotFoundMessage,
            >= 300 and < 400 when location.Contains("/closedform", StringComparison.OrdinalIgnoreCase) => ClosedMessage,
            (>= 300 and < 400) or 401 or 403 => SignInMessage,
            _ => $"Google replied with an error (HTTP {status}). Try again later.",
        };
    }

    /// <summary>Google's "no longer accepting responses" page has no form data in it.</summary>
    private static bool IsClosedPage(string? html) =>
        html is not null
        && !html.Contains("FB_PUBLIC_LOAD_DATA_", StringComparison.Ordinal)
        && html.Contains("no longer accepting responses", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<PersonResult>> SubmitAsync(RegistrationForm form, IReadOnlyList<ConfirmItem> items,
        IProgress<PersonResult>? progress, CancellationToken ct)
    {
        var results = new List<PersonResult>();
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            PersonResult result;
            if (item.IsBlocked)
            {
                result = new PersonResult(item, PersonResultState.Skipped, item.BlockedReason, clock.GetUtcNow());
            }
            else
            {
                progress?.Report(new PersonResult(item, PersonResultState.Sending, null, clock.GetUtcNow()));
                result = await SendOneAsync(form, item, ct).ConfigureAwait(false);
            }
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    private async Task<PersonResult> SendOneAsync(RegistrationForm form, ConfirmItem item, CancellationToken ct)
    {
        PersonResult Failed(string error) => new(item, PersonResultState.Failed, error, clock.GetUtcNow());
        if (!FormAddress.IsAllowed(form.ResponseUrl))
            return Failed("Couldn't send: the form's submit address isn't a Google Forms address.");
        try
        {
            var fields = PayloadBuilder.Build(form, item.Submission!);
            var reply = await client.PostAsync(form.ResponseUrl, fields, ct).ConfigureAwait(false);
            return SubmissionResultDetector.Classify(reply.StatusCode, reply.Body) switch
            {
                SubmissionOutcome.Success => new PersonResult(item, PersonResultState.Submitted, null, clock.GetUtcNow()),
                SubmissionOutcome.Rejected => Failed("Google didn't accept the response. The form may have changed."),
                _ => Failed($"Unexpected reply from Google (HTTP {reply.StatusCode})."),
            };
        }
        catch (HttpRequestException ex)
        {
            return Failed($"Couldn't reach Google: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failed(TimeoutMessage);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failed($"Couldn't send: {ex.Message}");
        }
    }
}
