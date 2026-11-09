using System.Net.Http;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;

namespace WicklowArchery.Core.Registration;

public sealed record FormLoadResult(FormCheck? Check, string? Error);

public enum PersonResultState { Pending, Sending, Submitted, Failed, Skipped }

public sealed record PersonResult(ConfirmItem Item, PersonResultState State, string? Error, DateTimeOffset At);

public sealed class RegistrationService(IFormClient client, TimeProvider clock)
{
    public async Task<FormLoadResult> LoadFormAsync(string formUrl, CancellationToken ct)
    {
        try
        {
            var html = await client.GetAsync(new Uri(formUrl), ct).ConfigureAwait(false);
            return new FormLoadResult(FormMapper.Map(FormParser.Parse(html)), null);
        }
        catch (HttpRequestException)
        {
            return new FormLoadResult(null, "Couldn't reach the form. Check your internet connection.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FormLoadResult(null, "Couldn't reach the form. Check your internet connection.");
        }
        catch (FormParseException)
        {
            return new FormLoadResult(null, "The form page couldn't be read. Google may have changed its format.");
        }
    }

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
            return Failed("Couldn't reach Google: the request timed out.");
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
