using System;
using System.Collections.Generic;
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

public class PeopleViewModelTests
{
    private sealed class FakeUrlOpener : IUrlOpener
    {
        public void Open(Uri url) { }
    }

    private sealed class FailableProtector : WicklowArchery.Core.Data.IDataProtector
    {
        public bool Fail { get; set; }
        public byte[] Protect(byte[] data) => Fail ? throw new System.IO.IOException("disk full") : (byte[])data.Clone();
        public byte[] Unprotect(byte[] data) => (byte[])data.Clone();
    }

    private sealed class Env
    {
        public required MainViewModel Vm { get; init; }
        public required AppDataStore Store { get; init; }
        public required PeopleViewModel People { get; init; }
    }

    private static async Task<Env> CreateAsync(int peopleCount = 2, IDataProtector? protector = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WicklowArcheryTests-" + Guid.NewGuid());
        var store = new AppDataStore(dir, protector ?? new PassThroughProtector());
        var data = AppData.CreateDefault();
        data.Settings.FormUrl = FormUrls.TestForm;
        for (var i = 1; i <= peopleCount; i++)
            data.People.Add(new Person { FullName = "Person " + i, ClubId = data.Clubs[0].Id, ClubNumber = "WA-00" + i, PaymentMethod = "", UsualDays = [DayOfWeek.Monday] });
        store.Save(data);

        var client = new FakeFormClient { GetBody = Fixtures.Read("test-form-2026-11-09.html") };
        var service = new RegistrationService(client, new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero)));
        var vm = new MainViewModel(service, store, new FakeUrlOpener());
        await vm.InitializeAsync();
        return new Env { Vm = vm, Store = store, People = new PeopleViewModel(vm) };
    }

    [Fact]
    public async Task Add_CreatesPersonWithDefaultClub_AndSelectsIt()
    {
        var env = await CreateAsync();
        env.People.AddCommand.Execute(null);

        var added = env.People.Selected!;
        Assert.Equal(3, env.People.People.Count);
        Assert.Same(env.People.People[2], added);
        Assert.Equal("", added.FullName);
        Assert.Equal("", added.ClubNumber);
        Assert.Equal("", added.Person.PaymentMethod);
        Assert.Empty(added.Person.UsualDays);
        Assert.True(added.Club!.IsDefault);
        Assert.Equal(env.Vm.Data.Clubs[0].Id, added.Person.ClubId);
        Assert.Contains(added.Person, env.Vm.Data.People);
        Assert.Equal(3, env.Store.Load().Data.People.Count);
    }

    [Fact]
    public async Task EditName_SavesToStore()
    {
        var env = await CreateAsync();
        env.People.Selected = env.People.People[0];
        env.People.Selected.FullName = "Renamed Archer";
        env.People.Selected.ClubNumber = "WA-999";

        var reloaded = env.Store.Load().Data.People.Single(p => p.Id == env.People.Selected.Person.Id);
        Assert.Equal("Renamed Archer", reloaded.FullName);
        Assert.Equal("WA-999", reloaded.ClubNumber);
    }

    [Fact]
    public async Task ToggleDayChip_UpdatesUsualDays()
    {
        var env = await CreateAsync();
        var editor = env.People.People[0];
        Assert.Equal(7, editor.Days.Count);
        Assert.Equal(DayOfWeek.Monday, editor.Days[0].Day);
        Assert.Equal(DayOfWeek.Sunday, editor.Days[6].Day);
        Assert.True(editor.Days[0].IsSelected);

        editor.Days[6].IsSelected = true;
        editor.Days[2].IsSelected = true;
        editor.Days[0].IsSelected = false;

        Assert.Equal([DayOfWeek.Wednesday, DayOfWeek.Sunday], editor.Person.UsualDays);
        var reloaded = env.Store.Load().Data.People.Single(p => p.Id == editor.Person.Id);
        Assert.Equal([DayOfWeek.Wednesday, DayOfWeek.Sunday], reloaded.UsualDays);
    }

    [Fact]
    public async Task Remove_DeletesAndSaves()
    {
        var env = await CreateAsync();
        var first = env.People.People[0];
        env.People.Selected = first;
        env.Vm.Data.History.Add(new HistoryEntry { PersonId = first.Person.Id, SessionText = "x", At = DateTimeOffset.Now, Succeeded = true });

        env.People.RemoveCommand.Execute(null);

        Assert.Single(env.People.People);
        Assert.Same(env.People.People[0], env.People.Selected);
        Assert.DoesNotContain(env.Vm.Data.People, p => p.Id == first.Person.Id);
        Assert.Single(env.Store.Load().Data.People);
        Assert.Single(env.Vm.Data.History);

        env.People.RemoveCommand.Execute(null);
        Assert.Null(env.People.Selected);
        Assert.Empty(env.Store.Load().Data.People);
    }

    [Fact]
    public async Task SavedPaymentNotOnForm_StillShownWithWarning()
    {
        var env = await CreateAsync();
        var person = env.People.People[0].Person;
        person.PaymentMethod = "Old option";
        var fresh = new PersonEditorViewModel(person, env.Vm);

        Assert.Equal("Old option", fresh.PaymentMethod);
        Assert.Equal("Old option", fresh.PaymentOptions[0]);
        Assert.Equal(env.Vm.Form!.PaymentOptions.Count + 1, fresh.PaymentOptions.Count);
        Assert.True(fresh.PaymentNotOnForm);

        var onForm = new PersonEditorViewModel(
            new Person { ClubId = env.Vm.Data.Clubs[0].Id, PaymentMethod = env.Vm.Form.PaymentOptions[0] }, env.Vm);
        Assert.False(onForm.PaymentNotOnForm);
        Assert.Equal(env.Vm.Form.PaymentOptions.Count, onForm.PaymentOptions.Count);
    }

    [Fact]
    public async Task LoanOptions_StartWithNoLoanBow_AndKeepUnknownSavedValue()
    {
        var env = await CreateAsync();
        var editor = env.People.People[0];
        Assert.Null(editor.LoanOptions[0]);
        Assert.False(editor.LoanNotOnForm);

        editor.LoanIndex = 1;
        Assert.Equal(env.Vm.Form!.LoanOptions[0], editor.Person.LoanBow);
        editor.LoanIndex = 0;
        Assert.Null(editor.Person.LoanBow);

        var odd = new PersonEditorViewModel(new Person { ClubId = env.Vm.Data.Clubs[0].Id, LoanBow = "Retired bow" }, env.Vm);
        Assert.Equal("Retired bow", odd.LoanBow);
        Assert.True(odd.LoanNotOnForm);
    }

    [Fact]
    public async Task Hints_ShowForEmptyRequiredFields()
    {
        var env = await CreateAsync();
        env.People.AddCommand.Execute(null);
        var e = env.People.Selected!;
        Assert.Equal("Needed for the form", e.NameHint);
        Assert.Equal("Needed for the form", e.NumberHint);
        Assert.Equal("Pick how this person pays", e.PaymentHint);
        e.FullName = "A";
        e.ClubNumber = "1";
        e.PaymentMethod = env.Vm.Form!.PaymentOptions[0];
        Assert.Null(e.NameHint);
        Assert.Null(e.NumberHint);
        Assert.Null(e.PaymentHint);
    }

    [Fact]
    public async Task PeopleChanged_UpdatesEmptyStateAndReason()
    {
        var env = await CreateAsync(peopleCount: 0);
        Assert.True(env.Vm.ShowEmptyState);
        Assert.Equal("Add a person first.", env.Vm.CompleteFormDisabledReason);

        var raised = new List<string?>();
        env.Vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        env.People.AddCommand.Execute(null);

        Assert.False(env.Vm.ShowEmptyState);
        Assert.Contains(nameof(MainViewModel.ShowEmptyState), raised);
        Assert.Contains(nameof(MainViewModel.CompleteFormDisabledReason), raised);
        Assert.NotEqual("Add a person first.", env.Vm.CompleteFormDisabledReason);
    }

    [Fact]
    public async Task PeopleChanged_ReloadsWeekGrid()
    {
        var env = await CreateAsync(peopleCount: 1);
        Assert.Single(env.Vm.Week.Rows);
        env.People.AddCommand.Execute(null);
        Assert.Equal(2, env.Vm.Week.Rows.Count);
        env.People.Selected!.Days[0].IsSelected = true;
        Assert.True(env.Vm.Week.Rows[1].Cells[0].IsTicked);
        env.People.RemoveCommand.Execute(null);
        Assert.Single(env.Vm.Week.Rows);
    }

    [Fact]
    public async Task FirstRunAddButton_AddsPersonAndOpensPeople()
    {
        var env = await CreateAsync(peopleCount: 0);
        env.Vm.AddFirstPersonCommand.Execute(null);

        Assert.Equal(Page.People, env.Vm.CurrentPage);
        Assert.Single(env.Vm.Data.People);
        Assert.Same(env.Vm.People.Selected!.Person, env.Vm.Data.People[0]);
        Assert.False(env.Vm.ShowEmptyState);
    }

    [Fact]
    public async Task Edit_WhenSaveFails_DoesNotShowSaved()
    {
        var protector = new FailableProtector();
        var env = await CreateAsync(protector: protector);
        protector.Fail = true;

        env.People.People[0].FullName = "Cannot save";

        Assert.False(env.People.ShowSaved);
        Assert.StartsWith("Couldn't save your details", env.Vm.Notice);

        protector.Fail = false;
        env.People.People[0].FullName = "Can save";
        Assert.True(env.People.ShowSaved);
    }

    [Fact]
    public async Task EditName_KeepsTicksOnGrid()
    {
        var env = await CreateAsync();
        var row = env.Vm.Week.Rows[0];
        row.Cells[2].IsTicked = true;
        env.Vm.Week.Rows[1].Cells[1].IsTicked = true;
        var ticked = env.Vm.Week.RegistrationCount;

        env.People.People[0].FullName = "Renamed";

        Assert.Same(row, env.Vm.Week.Rows[0]);
        Assert.Equal("Renamed", row.Name);
        Assert.True(row.Cells[2].IsTicked);
        Assert.True(env.Vm.Week.Rows[1].Cells[1].IsTicked);
        Assert.Equal(ticked, env.Vm.Week.RegistrationCount);
    }

    [Fact]
    public async Task EditUsualDays_KeepsOtherPeoplesTicks()
    {
        var env = await CreateAsync();
        // Person 2 un-ticks their pre-tick and ticks another session by hand.
        env.Vm.Week.Rows[1].Cells[0].IsTicked = false;
        env.Vm.Week.Rows[1].Cells[2].IsTicked = true;

        env.People.People[0].Days[2].IsSelected = true; // Wednesday for person 1

        Assert.False(env.Vm.Week.Rows[1].Cells[0].IsTicked);
        Assert.True(env.Vm.Week.Rows[1].Cells[2].IsTicked);
        Assert.True(env.Vm.Week.Rows[0].Cells[1].IsTicked); // person 1's new usual day is pre-ticked
    }

    [Fact]
    public async Task LeavingPeoplePage_CancelsRemovePrompt()
    {
        var env = await CreateAsync();
        env.Vm.CurrentPage = Page.People;
        env.People.AskRemoveCommand.Execute(null);
        Assert.True(env.People.Selected!.IsConfirmingRemove);
        env.Vm.CurrentPage = Page.Week;
        Assert.False(env.People.Selected.IsConfirmingRemove);
    }
}
