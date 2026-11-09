using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WicklowArchery.App.Services;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Registration;
using WicklowArchery.Tests.Fakes;
using Xunit;

namespace WicklowArchery.Tests.App;

public class SettingsViewModelTests
{
    private sealed class FakeUrlOpener : IUrlOpener
    {
        public void Open(Uri url) { }
    }

    private sealed class Env
    {
        public required MainViewModel Vm { get; init; }
        public required AppDataStore Store { get; init; }
        public required FakeFormClient Client { get; init; }
        public SettingsViewModel Settings => Vm.Settings;
    }

    private static async Task<Env> CreateAsync(string formUrl = FormUrls.TestForm, int clubs = 2, int peopleInFirstClub = 0)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WicklowArcheryTests-" + Guid.NewGuid());
        var store = new AppDataStore(dir, new PassThroughProtector());
        var data = AppData.CreateDefault();
        data.Settings.FormUrl = formUrl;
        for (var i = 2; i <= clubs; i++) data.Clubs.Add(new Club { Name = "Club " + i });
        for (var i = 1; i <= peopleInFirstClub; i++)
            data.People.Add(new Person { FullName = "Person " + i, ClubId = data.Clubs[0].Id, ClubNumber = "WA-00" + i, PaymentMethod = "" });
        store.Save(data);

        var client = new FakeFormClient { GetBody = Fixtures.Read("test-form-2026-11-09.html") };
        var service = new RegistrationService(client, new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero)));
        var vm = new MainViewModel(service, store, new FakeUrlOpener());
        await vm.InitializeAsync();
        client.Gets.Clear();
        return new Env { Vm = vm, Store = store, Client = client };
    }

    [Fact]
    public async Task SetDefault_LeavesExactlyOneDefault()
    {
        var env = await CreateAsync();
        var second = env.Settings.Clubs[1];

        env.Settings.SetDefaultClubCommand.Execute(second);

        Assert.Equal(1, env.Vm.Data.Clubs.Count(c => c.IsDefault));
        Assert.True(second.Club.IsDefault);
        Assert.True(second.IsDefault);
        Assert.False(env.Settings.Clubs[0].IsDefault);
        Assert.Equal(1, env.Store.Load().Data.Clubs.Count(c => c.IsDefault));
    }

    [Fact]
    public async Task RemoveClubInUse_BlockedWithMessage()
    {
        var env = await CreateAsync(peopleInFirstClub: 3);
        env.Settings.RemoveClubCommand.Execute(env.Settings.Clubs[0]);

        Assert.Equal(2, env.Vm.Data.Clubs.Count);
        Assert.Equal("In use by 3 people.", env.Settings.ClubMessage);
        Assert.Equal("In use by 3 people.", env.Settings.Clubs[0].Message);
    }

    [Fact]
    public async Task RemoveClubInUse_OnePerson_UsesSingular()
    {
        var env = await CreateAsync(peopleInFirstClub: 1);
        env.Settings.RemoveClubCommand.Execute(env.Settings.Clubs[0]);
        Assert.Equal("In use by 1 person.", env.Settings.ClubMessage);
    }

    [Fact]
    public async Task RemoveLastClub_Blocked()
    {
        var env = await CreateAsync(clubs: 1);
        env.Settings.RemoveClubCommand.Execute(env.Settings.Clubs[0]);

        Assert.Single(env.Vm.Data.Clubs);
        Assert.Equal("You need at least one club.", env.Settings.ClubMessage);
    }

    [Fact]
    public async Task RemoveDefaultClub_FirstRemainingBecomesDefault()
    {
        var env = await CreateAsync(clubs: 3);
        env.Settings.RemoveClubCommand.Execute(env.Settings.Clubs[0]);

        Assert.Equal(2, env.Vm.Data.Clubs.Count);
        Assert.Equal(1, env.Vm.Data.Clubs.Count(c => c.IsDefault));
        Assert.True(env.Vm.Data.Clubs[0].IsDefault);
        Assert.Equal("Club 2", env.Vm.Data.Clubs[0].Name);
        Assert.True(env.Settings.Clubs[0].IsDefault);
        Assert.Equal(2, env.Store.Load().Data.Clubs.Count);
    }

    [Fact]
    public async Task AskRemove_ShowsConfirm_AndCancelHidesIt()
    {
        var env = await CreateAsync();
        var row = env.Settings.Clubs[1];
        env.Settings.AskRemoveClubCommand.Execute(row);
        Assert.True(row.IsConfirmingRemove);
        Assert.Equal("Remove Club 2?", row.RemovePrompt);
        env.Settings.CancelRemoveClubCommand.Execute(row);
        Assert.False(row.IsConfirmingRemove);
        Assert.Equal(2, env.Vm.Data.Clubs.Count);
    }

    [Fact]
    public async Task AskRemove_InUse_DoesNotAsk()
    {
        var env = await CreateAsync(peopleInFirstClub: 2);
        var row = env.Settings.Clubs[0];
        env.Settings.AskRemoveClubCommand.Execute(row);
        Assert.False(row.IsConfirmingRemove);
        Assert.Equal("In use by 2 people.", row.Message);
    }

    [Fact]
    public async Task Rename_SavesImmediately()
    {
        var env = await CreateAsync();
        env.Settings.Clubs[1].Name = "Bray Bowmen";
        Assert.Equal("Bray Bowmen", env.Store.Load().Data.Clubs[1].Name);
    }

    [Fact]
    public async Task RenameToEmpty_NotSaved()
    {
        var env = await CreateAsync();
        var row = env.Settings.Clubs[1];
        row.Name = "   ";

        Assert.Equal("Club 2", env.Vm.Data.Clubs[1].Name);
        Assert.Equal("Club 2", env.Store.Load().Data.Clubs[1].Name);
        Assert.Equal("A club needs a name.", env.Settings.ClubMessage);

        row.RevertIfEmpty();
        Assert.Equal("Club 2", row.Name);
    }

    [Fact]
    public async Task AddClub_AppearsInPeopleEditorClubList()
    {
        var env = await CreateAsync(peopleInFirstClub: 1);
        var editor = env.Vm.People.People[0];
        var before = editor.Club;

        env.Settings.AddClubCommand.Execute(null);

        var added = env.Settings.Clubs[^1];
        Assert.Equal("New club", added.Name);
        Assert.False(added.IsDefault);
        Assert.Contains(editor.Clubs, c => c.Id == added.Club.Id);
        Assert.Same(before, editor.Club);
        Assert.Equal(3, env.Store.Load().Data.Clubs.Count);
    }

    [Fact]
    public async Task UseTestForm_SetsUrlSavesAndRefreshes()
    {
        var env = await CreateAsync(FormUrls.ClubForm);
        Assert.False(env.Vm.IsTestForm);

        await env.Settings.UseTestFormCommand.ExecuteAsync(null);

        Assert.Equal(FormUrls.TestForm, env.Vm.Data.Settings.FormUrl);
        Assert.Equal(FormUrls.TestForm, env.Settings.FormUrl);
        Assert.Equal(FormUrls.TestForm, env.Store.Load().Data.Settings.FormUrl);
        Assert.Single(env.Client.Gets);
        Assert.True(env.Vm.IsTestForm);
    }

    [Fact]
    public async Task UseClubForm_ClearsTestBadge()
    {
        var env = await CreateAsync(FormUrls.TestForm);
        Assert.True(env.Vm.IsTestForm);

        await env.Settings.UseClubFormCommand.ExecuteAsync(null);

        Assert.False(env.Vm.IsTestForm);
        Assert.Equal(FormUrls.ClubForm, env.Store.Load().Data.Settings.FormUrl);
        Assert.Single(env.Client.Gets);
    }

    [Fact]
    public async Task InvalidFormUrl_NotSavedAndShowsError()
    {
        var env = await CreateAsync();
        env.Settings.FormUrl = "https://example.com/not-a-form";
        await env.Settings.CommitFormUrlAsync();

        Assert.Equal(FormUrls.TestForm, env.Vm.Data.Settings.FormUrl);
        Assert.Equal(FormUrls.TestForm, env.Store.Load().Data.Settings.FormUrl);
        Assert.Equal("That isn't a Google Form link. Paste the full docs.google.com/forms link.", env.Settings.FormUrlError);
        Assert.Empty(env.Client.Gets);
    }

    [Fact]
    public async Task TypedValidUrl_TrimmedSavedAndRefreshed()
    {
        var env = await CreateAsync(FormUrls.ClubForm);
        env.Settings.FormUrl = "  " + FormUrls.TestForm + "  ";
        await env.Settings.CommitFormUrlAsync();

        Assert.Equal(FormUrls.TestForm, env.Vm.Data.Settings.FormUrl);
        Assert.Null(env.Settings.FormUrlError);
        Assert.Single(env.Client.Gets);
    }

    [Fact]
    public async Task UnchangedFormUrl_DoesNothing()
    {
        var env = await CreateAsync();
        env.Settings.FormUrl = FormUrls.TestForm + " ";
        await env.Settings.CommitFormUrlAsync();
        Assert.Empty(env.Client.Gets);
    }

    [Fact]
    public async Task FormStatus_Text()
    {
        var env = await CreateAsync(FormUrls.ClubForm);
        Assert.Equal("Club form — registrations go to the club.", env.Settings.FormStatusText);
        Assert.False(env.Settings.IsFormStatusCaution);
        await env.Settings.UseTestFormCommand.ExecuteAsync(null);
        Assert.Equal("Test form — registrations go to the test copy, not the club.", env.Settings.FormStatusText);
        Assert.True(env.Settings.IsFormStatusCaution);
    }

    [Fact]
    public async Task LeavingSettings_CommitsPendingFormUrl()
    {
        var env = await CreateAsync(FormUrls.ClubForm);
        env.Vm.CurrentPage = Page.Settings;
        env.Settings.FormUrl = FormUrls.TestForm;
        env.Vm.CurrentPage = Page.Week;

        Assert.Equal(FormUrls.TestForm, env.Vm.Data.Settings.FormUrl);
        Assert.Equal(FormUrls.TestForm, env.Store.Load().Data.Settings.FormUrl);
    }
}
