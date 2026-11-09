using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WicklowArchery.App.Services;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;
using WicklowArchery.Tests.Fakes;
using Xunit;

namespace WicklowArchery.Tests.App;

public class ConfirmViewModelTests
{
    private const string NonMember = "NON CLUB MEMBER PAYMENT";

    private sealed class RecordingUrlOpener : IUrlOpener
    {
        public List<Uri> Opened { get; } = [];
        public void Open(Uri url) => Opened.Add(url);
    }

    private sealed class Env
    {
        public required MainViewModel Vm { get; init; }
        public required FakeFormClient Client { get; init; }
        public required AppDataStore Store { get; init; }
        public required RecordingUrlOpener Opener { get; init; }
        public required Person Member { get; init; }
        public required Person Guest { get; init; }
        public required Person Blocked { get; init; }
        public string Monday => Vm.Form!.Sessions[0];
        public string Wednesday => Vm.Form!.Sessions[1];
    }

    private static async Task<Env> CreateAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WicklowArcheryTests-" + Guid.NewGuid());
        var store = new AppDataStore(dir, new PassThroughProtector());
        var data = AppData.CreateDefault();
        data.Settings.FormUrl = FormUrls.TestForm;
        var club = data.Clubs[0];
        var member = new Person { FullName = "Alex Archer", ClubId = club.Id, ClubNumber = "WA-001", PaymentMethod = "", UsualDays = [] };
        var guest = new Person { FullName = "Guest Gail", ClubId = club.Id, ClubNumber = "WA-002", PaymentMethod = NonMember, UsualDays = [] };
        var blocked = new Person { FullName = "No Number", ClubId = club.Id, ClubNumber = "", PaymentMethod = "", UsualDays = [] };
        data.People.AddRange([member, guest, blocked]);
        store.Save(data);

        var client = new FakeFormClient
        {
            GetBody = Fixtures.Read("test-form-2026-11-09.html"),
            OnPost = (_, _) => Task.FromResult(new HttpPostResult(200, Fixtures.Read("test-form-response-ok.html"))),
        };
        var opener = new RecordingUrlOpener();
        var service = new RegistrationService(client, new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero)));
        var vm = new MainViewModel(service, store, opener);
        await vm.InitializeAsync();

        // The member payment option text comes from the form itself.
        var memberOption = vm.Form!.PaymentOptions.First(o => o != NonMember);
        vm.Data.People.Single(p => p.Id == member.Id).PaymentMethod = memberOption;
        vm.Data.People.Single(p => p.Id == blocked.Id).PaymentMethod = memberOption;
        vm.Week.Load(vm.Form, vm.Data);
        return new Env { Vm = vm, Client = client, Store = store, Opener = opener, Member = member, Guest = guest, Blocked = blocked };
    }

    private static void Tick(Env e, Person p, params string[] sessions)
    {
        var row = e.Vm.Week.Rows.Single(r => r.Person.Id == p.Id);
        foreach (var cell in row.Cells)
            cell.IsTicked = sessions.Contains(cell.SessionText);
    }

    private static ConfirmViewModel OpenConfirm(Env e)
    {
        e.Vm.CompleteFormCommand.Execute(null);
        return Assert.IsType<ConfirmViewModel>(e.Vm.Overlay);
    }

    [Fact]
    public async Task Confirm_ShowsRepeatsAndBlocked()
    {
        var env = await CreateAsync();
        var at = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
        env.Vm.Data.History.Add(new HistoryEntry { PersonId = env.Member.Id, SessionText = env.Monday, At = at, Succeeded = true });
        env.Vm.Week.Load(env.Vm.Form!, env.Vm.Data);
        Tick(env, env.Member, env.Monday, env.Wednesday);
        Tick(env, env.Blocked, env.Monday);

        var confirm = OpenConfirm(env);

        Assert.Equal("Ready to send", confirm.Title);
        Assert.Equal(2, confirm.People.Count);
        Assert.Equal(1, confirm.SendableCount);
        Assert.Equal("Submit (1)", confirm.SubmitText);
        var member = confirm.People[0];
        Assert.Equal("Alex Archer", member.Name);
        Assert.Equal(2, member.Sessions.Count);
        var expectedDate = at.ToLocalTime().ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Equal($"Already registered on {expectedDate}; will be sent again", member.Sessions[0].RepeatText);
        Assert.Null(member.Sessions[1].RepeatText);
        var blocked = confirm.People[1];
        Assert.True(blocked.IsBlocked);
        Assert.Equal("Missing club number.", blocked.BlockedReason);
        Assert.True(confirm.SubmitCommand.CanExecute(null));

        confirm.CancelCommand.Execute(null);
        Assert.Null(env.Vm.Overlay);
    }

    [Fact]
    public async Task Submit_InvokedTwiceWhileRunning_PostsOncePerPerson()
    {
        var env = await CreateAsync();
        var gate = new TaskCompletionSource();
        env.Client.OnPost = async (_, _) =>
        {
            await gate.Task;
            return new HttpPostResult(200, Fixtures.Read("test-form-response-ok.html"));
        };
        Tick(env, env.Member, env.Monday);
        Tick(env, env.Guest, env.Monday);
        var confirm = OpenConfirm(env);

        var first = confirm.SubmitCommand.ExecuteAsync(null);
        var second = confirm.SubmitCommand.ExecuteAsync(null);
        Assert.False(confirm.CancelCommand.CanExecute(null));
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(2, env.Client.Posts.Count);
    }

    [Fact]
    public async Task Submit_RecordsHistoryAndSaves()
    {
        var env = await CreateAsync();
        Tick(env, env.Member, env.Monday, env.Wednesday);
        var confirm = OpenConfirm(env);

        await confirm.SubmitCommand.ExecuteAsync(null);

        var reloaded = env.Store.Load().Data;
        Assert.Equal(2, reloaded.History.Count(h => h.PersonId == env.Member.Id && h.Succeeded));
        Assert.Contains(reloaded.History, h => h.SessionText == env.Monday);
        Assert.Contains(reloaded.History, h => h.SessionText == env.Wednesday);
    }

    [Fact]
    public async Task Submit_FailedPerson_HistoryNotSucceeded()
    {
        var env = await CreateAsync();
        env.Client.OnPost = (_, _) => throw new HttpRequestException("down");
        Tick(env, env.Member, env.Monday);
        var confirm = OpenConfirm(env);

        await confirm.SubmitCommand.ExecuteAsync(null);

        var results = Assert.IsType<ResultsViewModel>(env.Vm.Overlay);
        Assert.Equal(PersonResultState.Failed, results.Rows[0].State);
        Assert.NotNull(results.Rows[0].Error);
        var entry = Assert.Single(env.Store.Load().Data.History);
        Assert.False(entry.Succeeded);
        Assert.NotNull(entry.Error);
        Assert.True(results.IsFinished);
        Assert.Equal("Done", results.Title);
    }

    [Fact]
    public async Task Results_ShowSumUp_OnlyForNonMemberSubmitted()
    {
        var env = await CreateAsync();
        Tick(env, env.Member, env.Monday);
        await OpenConfirm(env).SubmitCommand.ExecuteAsync(null);
        Assert.False(Assert.IsType<ResultsViewModel>(env.Vm.Overlay).ShowSumUp);

        env.Vm.Overlay = null;
        Tick(env, env.Guest, env.Monday);
        await OpenConfirm(env).SubmitCommand.ExecuteAsync(null);
        Assert.True(Assert.IsType<ResultsViewModel>(env.Vm.Overlay).ShowSumUp);

        // A non-member whose send failed does not get the pay button.
        env.Vm.Overlay = null;
        env.Client.OnPost = (_, _) => throw new HttpRequestException("down");
        Tick(env, env.Guest, env.Wednesday);
        await OpenConfirm(env).SubmitCommand.ExecuteAsync(null);
        Assert.False(Assert.IsType<ResultsViewModel>(env.Vm.Overlay).ShowSumUp);
    }

    [Fact]
    public async Task PaySumUp_OpensFormSumUpUrl()
    {
        var env = await CreateAsync();
        Tick(env, env.Guest, env.Monday);
        await OpenConfirm(env).SubmitCommand.ExecuteAsync(null);
        var results = Assert.IsType<ResultsViewModel>(env.Vm.Overlay);

        results.PaySumUpCommand.Execute(null);

        Assert.Equal(env.Vm.Form!.SumUpUrl, Assert.Single(env.Opener.Opened));
        results.DoneCommand.Execute(null);
        Assert.Null(env.Vm.Overlay);
    }

    [Fact]
    public async Task AfterSubmit_GridShowsRegistered()
    {
        var env = await CreateAsync();
        Tick(env, env.Member, env.Monday);
        await OpenConfirm(env).SubmitCommand.ExecuteAsync(null);

        var cell = env.Vm.Week.Rows.Single(r => r.Person.Id == env.Member.Id).Cells.Single(c => c.SessionText == env.Monday);
        Assert.True(cell.IsRegistered);
    }

    [Fact]
    public async Task Close_WhileSubmitting_IsSubmittingTrue()
    {
        var env = await CreateAsync();
        var gate = new TaskCompletionSource();
        env.Client.OnPost = async (_, _) =>
        {
            await gate.Task;
            return new HttpPostResult(200, Fixtures.Read("test-form-response-ok.html"));
        };
        Tick(env, env.Member, env.Monday);
        var confirm = OpenConfirm(env);
        Assert.False(env.Vm.IsSubmitting);
        Assert.False(env.Vm.IsContentEnabled);

        var run = confirm.SubmitCommand.ExecuteAsync(null);

        Assert.True(env.Vm.IsSubmitting);
        Assert.False(env.Vm.IsContentEnabled);
        Assert.False(env.Vm.RefreshCommand.CanExecute(null));
        gate.SetResult();
        await run;
        Assert.False(env.Vm.IsSubmitting);
        Assert.True(env.Vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task AfterRun_FailedPersonKeepsTicks()
    {
        var env = await CreateAsync();
        env.Client.OnPost = (_, fields) =>
            fields.Any(f => f.Value == "Guest Gail")
                ? throw new HttpRequestException("down")
                : Task.FromResult(new HttpPostResult(200, Fixtures.Read("test-form-response-ok.html")));
        Tick(env, env.Member, env.Monday);
        Tick(env, env.Guest, env.Monday, env.Wednesday);
        await OpenConfirm(env).SubmitCommand.ExecuteAsync(null);

        var guestCells = env.Vm.Week.Rows.Single(r => r.Person.Id == env.Guest.Id).Cells;
        Assert.True(guestCells.Single(c => c.SessionText == env.Monday).IsTicked);
        Assert.True(guestCells.Single(c => c.SessionText == env.Wednesday).IsTicked);
        var memberCells = env.Vm.Week.Rows.Single(r => r.Person.Id == env.Member.Id).Cells;
        Assert.True(memberCells.Single(c => c.SessionText == env.Monday).IsRegistered);
        Assert.False(memberCells.Single(c => c.SessionText == env.Monday).IsTicked);
    }

    [Fact]
    public async Task Run_ExceptionAfterStart_ResetsIsSubmitting()
    {
        var env = await CreateAsync();
        Tick(env, env.Member, env.Monday);
        var confirm = OpenConfirm(env);
        // Corrupt a person after the confirm panel is built, so reloading the grid after the run throws.
        env.Vm.Data.People.Single(p => p.Id == env.Guest.Id).UsualDays = null!;

        await confirm.SubmitCommand.ExecuteAsync(null);

        var results = Assert.IsType<ResultsViewModel>(env.Vm.Overlay);
        Assert.False(env.Vm.IsSubmitting);
        Assert.True(results.IsFinished);
        Assert.True(results.DoneCommand.CanExecute(null));
        Assert.All(results.Rows, r => Assert.NotEqual(PersonResultState.Sending, r.State));
        Assert.True(env.Vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task Run_ExceptionBeforeOverlay_ResetsIsSubmitting()
    {
        var env = await CreateAsync();
        OpenConfirm(env);

        await env.Vm.SubmitAsync(env.Vm.Form!, null!);

        Assert.False(env.Vm.IsSubmitting);
        Assert.True(env.Vm.RefreshCommand.CanExecute(null));
        Assert.NotNull(env.Vm.Notice);
    }

    [Fact]
    public async Task CloseBlockedMessage_ShownOnResultsCard()
    {
        var env = await CreateAsync();
        var gate = new TaskCompletionSource();
        env.Client.OnPost = async (_, _) =>
        {
            await gate.Task;
            return new HttpPostResult(200, Fixtures.Read("test-form-response-ok.html"));
        };
        Tick(env, env.Member, env.Monday);
        var confirm = OpenConfirm(env);
        Assert.False(env.Vm.TryBlockClose());

        var run = confirm.SubmitCommand.ExecuteAsync(null);
        Assert.True(env.Vm.TryBlockClose());
        var results = Assert.IsType<ResultsViewModel>(env.Vm.Overlay);
        Assert.Equal("Sending in progress. Close the app when it finishes.", results.CloseBlockedMessage);

        gate.SetResult();
        await run;
        Assert.Null(results.CloseBlockedMessage);
        Assert.False(env.Vm.TryBlockClose());
    }
    [Fact]
    public async Task Send_FormIdMismatch_Refused()
    {
        var env = await CreateAsync();
        Tick(env, env.Member, env.Monday);
        var confirm = OpenConfirm(env);
        // The saved address no longer matches the form the panel was built from.
        env.Vm.Data.Settings.FormUrl = FormUrls.ClubForm;

        await confirm.SubmitCommand.ExecuteAsync(null);

        Assert.Empty(env.Client.Posts);
        Assert.NotNull(env.Vm.Notice);
        Assert.False(env.Vm.IsSubmitting);
        Assert.Empty(env.Store.Load().Data.History);
    }

    [Fact]
    public async Task Send_UsesFormFromConfirmPanel()
    {
        var env = await CreateAsync();
        Tick(env, env.Member, env.Monday);
        var confirmedUrl = env.Vm.Form!.ResponseUrl;
        var confirm = OpenConfirm(env);

        // A reload while the panel is open finds a changed page, so the main form goes away.
        env.Client.GetBody = "<html>nothing here</html>";
        await env.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(env.Vm.Form);

        await confirm.SubmitCommand.ExecuteAsync(null);

        var post = Assert.Single(env.Client.Posts);
        Assert.Equal(confirmedUrl, post.Url);
        var results = Assert.IsType<ResultsViewModel>(env.Vm.Overlay);
        Assert.Equal(PersonResultState.Submitted, results.Rows[0].State);
    }

    [Fact]
    public async Task CloseBlockedNotice_ClearedWhenRunEnds()
    {
        var env = await CreateAsync();
        var gate = new TaskCompletionSource();
        env.Client.OnPost = async (_, _) =>
        {
            await gate.Task;
            return new HttpPostResult(200, Fixtures.Read("test-form-response-ok.html"));
        };
        Tick(env, env.Member, env.Monday);
        var run = OpenConfirm(env).SubmitCommand.ExecuteAsync(null);
        Assert.True(env.Vm.TryBlockClose());
        Assert.Equal("Sending in progress. Close the app when it finishes.", env.Vm.Notice);

        gate.SetResult();
        await run;
        Assert.Null(env.Vm.Notice);
    }
}
