using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using Xunit;

namespace WicklowArchery.Tests.App;

public class WeekViewModelTests
{
    // Test snapshot sessions: Monday 9th, Wednesday 11th, Sunday 15th (in that order).
    private static RegistrationForm Form() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"))).Form!;

    private static Person P(string name, params DayOfWeek[] days) => new() { FullName = name, UsualDays = days.ToList() };

    private static AppData Data(params Person[] people) => new() { People = people.ToList() };

    private static WeekViewModel Loaded(RegistrationForm form, AppData data)
    {
        var vm = new WeekViewModel();
        vm.Load(form, data);
        return vm;
    }

    private static SessionCellViewModel Cell(WeekViewModel vm, Person p, DayOfWeek day) =>
        vm.Rows.Single(r => r.Person == p).Cells.Single(c => SessionText.DayOf(c.SessionText) == day);

    [Fact]
    public void Load_PreTicksUsualDaysAndCountsThem()
    {
        var a = P("A", DayOfWeek.Monday, DayOfWeek.Wednesday);
        var b = P("B", DayOfWeek.Sunday);
        var vm = Loaded(Form(), Data(a, b));

        Assert.Equal(3, vm.Headers.Count);
        Assert.Equal("Monday 9th", vm.Headers[0].Headline);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("A", vm.Rows[0].Name);
        Assert.All(vm.Rows, r => Assert.Equal(3, r.Cells.Count));
        Assert.True(Cell(vm, a, DayOfWeek.Monday).IsTicked);
        Assert.True(Cell(vm, a, DayOfWeek.Wednesday).IsTicked);
        Assert.False(Cell(vm, a, DayOfWeek.Sunday).IsTicked);
        Assert.True(Cell(vm, b, DayOfWeek.Sunday).IsTicked);
        Assert.Equal(2, vm.PeopleCount);
        Assert.Equal(3, vm.RegistrationCount);
        Assert.Equal("2 people · 3 registrations", vm.SummaryText);
    }

    [Fact]
    public void ToggleCell_UpdatesSummary()
    {
        var a = P("A", DayOfWeek.Monday, DayOfWeek.Wednesday);
        var b = P("B", DayOfWeek.Sunday);
        var vm = Loaded(Form(), Data(a, b));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Cell(vm, b, DayOfWeek.Sunday).IsTicked = false;

        Assert.Equal(1, vm.PeopleCount);
        Assert.Equal(2, vm.RegistrationCount);
        Assert.Equal("1 person · 2 registrations", vm.SummaryText);
        Assert.Contains(nameof(WeekViewModel.SummaryText), changed);
        Assert.Contains(nameof(WeekViewModel.PeopleCount), changed);
        Assert.Contains(nameof(WeekViewModel.RegistrationCount), changed);

        Cell(vm, b, DayOfWeek.Monday).IsTicked = true;
        Assert.Equal("2 people · 3 registrations", vm.SummaryText);
    }

    [Fact]
    public void Summary_Singular()
    {
        var vm = Loaded(Form(), Data(P("A", DayOfWeek.Sunday), P("B")));
        Assert.Equal("1 person · 1 registration", vm.SummaryText);
    }

    [Fact]
    public void Summary_NothingTicked()
    {
        var vm = Loaded(Form(), Data(P("A"), P("B")));
        Assert.Equal(0, vm.PeopleCount);
        Assert.Equal(0, vm.RegistrationCount);
        Assert.Equal("Nothing ticked yet", vm.SummaryText);
    }

    [Fact]
    public void RegisteredCell_TickedBecomesRepeat()
    {
        var form = Form();
        var a = P("A", DayOfWeek.Wednesday);
        var wed = form.Sessions.Single(s => SessionText.DayOf(s) == DayOfWeek.Wednesday);
        var entry = new HistoryEntry { PersonId = a.Id, SessionText = wed, Succeeded = true, At = DateTimeOffset.UtcNow };
        var data = Data(a);
        data.History.Add(entry);
        var vm = Loaded(form, data);
        var cell = Cell(vm, a, DayOfWeek.Wednesday);

        Assert.Same(entry, cell.AlreadyRegistered);
        Assert.False(cell.IsTicked);
        Assert.False(cell.IsRepeat);
        Assert.Equal("Nothing ticked yet", vm.SummaryText);

        var changed = new List<string?>();
        ((INotifyPropertyChanged)cell).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        cell.IsTicked = true;

        Assert.True(cell.IsRepeat);
        Assert.Contains(nameof(SessionCellViewModel.IsRepeat), changed);
        Assert.Equal("1 person · 1 registration", vm.SummaryText);
    }

    [Fact]
    public void GetSelections_OnlyPeopleWithTicks_InSessionOrder()
    {
        var a = P("A");
        var b = P("B");
        var c = P("C", DayOfWeek.Wednesday);
        var vm = Loaded(Form(), Data(a, b, c));
        // Tick B's Sunday before Monday: the result must still follow the form's order.
        Cell(vm, b, DayOfWeek.Sunday).IsTicked = true;
        Cell(vm, b, DayOfWeek.Monday).IsTicked = true;

        var selections = vm.GetSelections();

        Assert.Equal(2, selections.Count);
        Assert.Same(b, selections[0].Person);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], selections[0].Sessions.Select(s => SessionText.DayOf(s)!.Value));
        Assert.Same(c, selections[1].Person);
        Assert.Equal([DayOfWeek.Wednesday], selections[1].Sessions.Select(s => SessionText.DayOf(s)!.Value));
    }

    [Fact]
    public void NoSessions_HasSessionsFalse()
    {
        var vm = Loaded(Form() with { Sessions = [] }, Data(P("A", DayOfWeek.Monday)));
        Assert.False(vm.HasSessions);
        Assert.Empty(vm.Headers);
        Assert.Equal("Nothing ticked yet", vm.SummaryText);
        Assert.Empty(vm.GetSelections());

        vm.Load(Form(), Data(P("A", DayOfWeek.Monday)));
        Assert.True(vm.HasSessions);
    }
}
