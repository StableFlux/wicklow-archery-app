using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;
using WicklowArchery.Tests.Fakes;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class RegistrationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Club Club = new() { Name = "Wicklow Archery", IsDefault = true };

    private static RegistrationForm Form() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"))).Form!;

    private static (FakeFormClient Client, RegistrationService Service) Make()
    {
        var c = new FakeFormClient { GetBody = Fixtures.Read("test-form-2026-11-09.html") };
        return (c, new RegistrationService(c, new FixedTimeProvider(Now)));
    }

    private static Person P(RegistrationForm form, string name, bool blank = false) => new()
    {
        FullName = blank ? "" : name, ClubId = Club.Id, ClubNumber = "1", PaymentMethod = form.PaymentOptions[0],
    };

    private static IReadOnlyList<ConfirmItem> Items(RegistrationForm form, params Person[] people) =>
        ConfirmationBuilder.Build(form, people.Select(p => new Selection(p, [form.Sessions[0]])).ToList(), [Club], []);

    private sealed class SyncProgress(List<PersonResult> sink) : IProgress<PersonResult>
    {
        public void Report(PersonResult value) => sink.Add(value);
    }

    [Fact]
    public async Task Load_NetworkError_ReturnsError()
    {
        var (c, s) = Make();
        c.GetException = new HttpRequestException("boom");
        var r = await s.LoadFormAsync(FormUrls.TestForm, CancellationToken.None);
        Assert.Null(r.Check);
        Assert.Equal("Couldn't reach the form. Check your internet connection.", r.Error);
    }

    [Fact]
    public async Task Load_TestSnapshot_ReturnsUsableCheck()
    {
        var (_, s) = Make();
        var r = await s.LoadFormAsync(FormUrls.TestForm, CancellationToken.None);
        Assert.Null(r.Error);
        Assert.True(r.Check!.IsUsable);
    }

    [Fact]
    public async Task Load_GarbagePage_ReturnsParseError()
    {
        var (c, s) = Make();
        c.GetBody = "<html>nothing here</html>";
        var r = await s.LoadFormAsync(FormUrls.TestForm, CancellationToken.None);
        Assert.Null(r.Check);
        Assert.Equal("The form page couldn't be read. Google may have changed its format.", r.Error);
    }

    [Fact]
    public async Task Submit_SendsOnePostPerUnblockedPerson_InOrder()
    {
        var (c, s) = Make();
        var form = Form();
        var items = Items(form, P(form, "Ann"), P(form, "Bob"));
        var results = await s.SubmitAsync(form, items, null, CancellationToken.None);
        Assert.Equal(2, c.Posts.Count);
        Assert.All(c.Posts, p => Assert.Equal(form.ResponseUrl, p.Url));
        Assert.Contains(c.Posts[0].Fields, f => f.Value == "Ann");
        Assert.Contains(c.Posts[1].Fields, f => f.Value == "Bob");
        Assert.All(results, r => Assert.Equal(PersonResultState.Submitted, r.State));
        Assert.All(results, r => Assert.Equal(Now, r.At));
    }

    [Fact]
    public async Task Submit_BlockedItem_SkippedWithoutPost()
    {
        var (c, s) = Make();
        var form = Form();
        var items = Items(form, P(form, "", blank: true));
        var results = await s.SubmitAsync(form, items, null, CancellationToken.None);
        Assert.Empty(c.Posts);
        Assert.Equal(PersonResultState.Skipped, results[0].State);
        Assert.Equal(items[0].BlockedReason, results[0].Error);
    }

    [Fact]
    public async Task Submit_NetworkErrorForOnePerson_ContinuesWithOthers()
    {
        var (c, s) = Make();
        c.OnPost = (i, _) => i == 1 ? throw new HttpRequestException("offline")
            : Task.FromResult(new HttpPostResult(200, "Your response has been recorded."));
        var form = Form();
        var results = await s.SubmitAsync(form, Items(form, P(form, "A"), P(form, "B"), P(form, "C")), null, CancellationToken.None);
        Assert.Equal([PersonResultState.Submitted, PersonResultState.Failed, PersonResultState.Submitted],
            results.Select(r => r.State).ToArray());
        Assert.StartsWith("Couldn't reach Google:", results[1].Error);
        Assert.Equal(3, c.Posts.Count);
    }

    [Fact]
    public async Task Submit_RejectedReply_Failed()
    {
        var (c, s) = Make();
        c.OnPost = (_, _) => Task.FromResult(new HttpPostResult(400, Fixtures.Read("test-form-response-error.html")));
        var form = Form();
        var results = await s.SubmitAsync(form, Items(form, P(form, "A")), null, CancellationToken.None);
        Assert.Equal(PersonResultState.Failed, results[0].State);
        Assert.Equal("Google didn't accept the response. The form may have changed.", results[0].Error);
    }

    [Fact]
    public async Task Submit_UnexpectedReply_Failed()
    {
        var (c, s) = Make();
        c.OnPost = (_, _) => Task.FromResult(new HttpPostResult(503, "<html>down</html>"));
        var form = Form();
        var results = await s.SubmitAsync(form, Items(form, P(form, "A")), null, CancellationToken.None);
        Assert.Equal("Unexpected reply from Google (HTTP 503).", results[0].Error);
    }

    [Fact]
    public async Task Submit_ReportsSendingThenFinalState()
    {
        var (c, s) = Make();
        var form = Form();
        var seen = new List<PersonResult>();
        await s.SubmitAsync(form, Items(form, P(form, "A")), new SyncProgress(seen), CancellationToken.None);
        Assert.Equal([PersonResultState.Sending, PersonResultState.Submitted], seen.Select(r => r.State).ToArray());
    }

    [Fact]
    public async Task Load_NonGoogleUrl_ReturnsErrorWithoutRequest()
    {
        var (c, s) = Make();
        var r = await s.LoadFormAsync("https://evil.example/forms/d/e/x/viewform", CancellationToken.None);
        Assert.Null(r.Check);
        Assert.Equal("That form address isn't a Google Form link. Use the full docs.google.com/forms link.", r.Error);
        Assert.Empty(c.Gets);
    }

    [Fact]
    public async Task Load_ResponseUrlForDifferentForm_ReturnsParseError()
    {
        var (c, s) = Make();
        var r = await s.LoadFormAsync(FormUrls.ClubForm, CancellationToken.None);
        Assert.Null(r.Check);
        Assert.Equal("The form page couldn't be read. Google may have changed its format.", r.Error);
        Assert.Empty(c.Posts);
    }

    [Fact]
    public async Task Submit_ResponseUrlNotAllowed_FailsWithoutPost()
    {
        var (c, s) = Make();
        var form = Form() with { ResponseUrl = new Uri("https://evil.example/formResponse") };
        var results = await s.SubmitAsync(form, Items(form, P(form, "A")), null, CancellationToken.None);
        Assert.Empty(c.Posts);
        Assert.Equal(PersonResultState.Failed, results[0].State);
        Assert.Equal("Couldn't send: the form's submit address isn't a Google Forms address.", results[0].Error);
    }

    [Fact]
    public async Task Submit_RedirectReply_FailedUnexpected302()
    {
        var (c, s) = Make();
        c.OnPost = (_, _) => Task.FromResult(new HttpPostResult(302, ""));
        var form = Form();
        var results = await s.SubmitAsync(form, Items(form, P(form, "A")), null, CancellationToken.None);
        Assert.Equal(PersonResultState.Failed, results[0].State);
        Assert.Equal("Unexpected reply from Google (HTTP 302).", results[0].Error);
    }
}
