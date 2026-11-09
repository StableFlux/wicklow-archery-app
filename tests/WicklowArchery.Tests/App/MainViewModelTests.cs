using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WicklowArchery.App.Services;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Registration;
using WicklowArchery.Tests.Fakes;
using Xunit;

namespace WicklowArchery.Tests.App;

public class MainViewModelTests
{
    private sealed class FakeUrlOpener : IUrlOpener
    {
        public void Open(Uri url) { }
    }

    private static (MainViewModel Vm, FakeFormClient Client) Create(string formUrl = FormUrls.TestForm, bool withPeople = true,
        string? body = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WicklowArcheryTests-" + Guid.NewGuid());
        var store = new AppDataStore(dir, new PassThroughProtector());
        var data = AppData.CreateDefault();
        data.Settings.FormUrl = formUrl;
        if (withPeople)
            data.People.Add(new Person { FullName = "Alex Archer", ClubId = data.Clubs[0].Id, UsualDays = [DayOfWeek.Monday] });
        store.Save(data);

        var client = new FakeFormClient { GetBody = body ?? Fixtures.Read("test-form-2026-11-09.html") };
        var service = new RegistrationService(client, new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero)));
        return (new MainViewModel(service, store, new FakeUrlOpener()), client);
    }

    [Fact]
    public async Task Init_TestSnapshot_UpToDateWithSessionCount()
    {
        var (vm, _) = Create();
        Assert.Equal(FormStatus.Checking, vm.Status);
        await vm.InitializeAsync();
        Assert.Equal(FormStatus.UpToDate, vm.Status);
        Assert.Equal("This week's form ✓ · 3 sessions", vm.StatusText);
        Assert.NotNull(vm.Form);
        Assert.Empty(vm.Problems);
        Assert.Equal(3, vm.Week.Headers.Count);
    }

    [Fact]
    public async Task Init_NetworkError_OfflineAndDisabled()
    {
        var (vm, client) = Create();
        client.GetException = new HttpRequestException("down");
        await vm.InitializeAsync();
        Assert.Equal(FormStatus.Offline, vm.Status);
        Assert.Equal("Can't reach the form", vm.StatusText);
        Assert.Equal("Can't reach the form.", vm.CompleteFormDisabledReason);
        Assert.False(vm.CompleteFormCommand.CanExecute(null));
    }

    [Fact]
    public async Task Init_ChangedForm_ChangedWithProblemsAndDisabled()
    {
        var body = Regex.Replace(Fixtures.Read("test-form-2026-11-09.html"), "Full Name", "Nickname", RegexOptions.IgnoreCase);
        var (vm, _) = Create(body: body);
        await vm.InitializeAsync();
        Assert.Equal(FormStatus.Changed, vm.Status);
        Assert.Equal("Form changed: Complete Form disabled", vm.StatusText);
        Assert.NotEmpty(vm.Problems);
        Assert.Equal("The form has changed. Click the status for details.", vm.CompleteFormDisabledReason);
        Assert.False(vm.CompleteFormCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_AfterOffline_RecoversToUpToDate()
    {
        var (vm, client) = Create();
        client.GetException = new HttpRequestException("down");
        await vm.InitializeAsync();
        Assert.Equal(FormStatus.Offline, vm.Status);

        client.GetException = null;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(FormStatus.UpToDate, vm.Status);
        Assert.Equal(2, client.Gets.Count);
    }

    [Fact]
    public void IsTestForm_TrueForTestUrl_FalseForClubUrl()
    {
        Assert.True(Create(FormUrls.TestForm).Vm.IsTestForm);
        Assert.False(Create(FormUrls.ClubForm).Vm.IsTestForm);
    }

    [Fact]
    public async Task NoPeople_ShowEmptyState_AndReasonAddPersonFirst()
    {
        var (vm, _) = Create(withPeople: false);
        await vm.InitializeAsync();
        Assert.True(vm.ShowEmptyState);
        Assert.Equal("Add a person first.", vm.CompleteFormDisabledReason);
        Assert.False(vm.CompleteFormCommand.CanExecute(null));
    }

    [Fact]
    public async Task NothingTicked_ReasonTickAtLeastOne()
    {
        var (vm, _) = Create();
        await vm.InitializeAsync();
        foreach (var cell in vm.Week.Rows.SelectMany(r => r.Cells))
            cell.IsTicked = false;
        Assert.False(vm.ShowEmptyState);
        Assert.Equal("Tick at least one session.", vm.CompleteFormDisabledReason);

        vm.Week.Rows[0].Cells[0].IsTicked = true;
        Assert.Null(vm.CompleteFormDisabledReason);
        Assert.True(vm.CompleteFormCommand.CanExecute(null));
    }
}
