using System;
using System.Collections.Generic;
using System.Linq;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class WeekPlannerTests
{
    private static IReadOnlyList<string> Sessions() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("club-form-2026-11-09.html"))).Form!.Sessions;

    private static readonly string Club = FormHistory.ClubFormId;

    private static Person P(params DayOfWeek[] days) => new() { FullName = "A", UsualDays = days.ToList() };

    private static HistoryEntry H(Person p, string s, bool ok) =>
        new() { PersonId = p.Id, SessionText = s, Succeeded = ok, At = DateTimeOffset.UtcNow };

    [Fact]
    public void Build_UsualWednesday_PreTicksWednesdayOnly()
    {
        var s = Sessions();
        var cells = WeekPlanner.Build(s, [P(DayOfWeek.Wednesday)], [], Club);
        Assert.Equal(s.Count, cells.Count);
        var ticked = cells.Where(c => c.PreTicked).Select(c => c.SessionText).ToList();
        Assert.NotEmpty(ticked);
        Assert.All(ticked, t => Assert.Equal(DayOfWeek.Wednesday, SessionText.DayOf(t)));
        Assert.Equal(s.Count(x => SessionText.DayOf(x) == DayOfWeek.Wednesday), ticked.Count);
    }

    [Fact]
    public void Build_SeveralRegistrations_UsesLatest()
    {
        var s = Sessions();
        var p = P();
        var wed = s.First(x => SessionText.DayOf(x) == DayOfWeek.Wednesday);
        var older = new HistoryEntry { PersonId = p.Id, SessionText = wed, Succeeded = true, At = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero) };
        var newer = new HistoryEntry { PersonId = p.Id, SessionText = wed, Succeeded = true, At = new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero) };
        var cell = WeekPlanner.Build(s, [p], [newer, older], Club).Single(c => c.SessionText == wed);
        Assert.Same(newer, cell.AlreadyRegistered);
        cell = WeekPlanner.Build(s, [p], [older, newer], Club).Single(c => c.SessionText == wed);
        Assert.Same(newer, cell.AlreadyRegistered);
    }

    [Fact]
    public void Build_SessionWithoutDayName_NeverPreTicked()
    {
        var cells = WeekPlanner.Build(["Special event"], [P(Enum.GetValues<DayOfWeek>())], [], Club);
        Assert.False(Assert.Single(cells).PreTicked);
    }

    [Fact]
    public void Build_AlreadyRegistered_MarkedAndNotPreTicked()
    {
        var s = Sessions();
        var p = P(DayOfWeek.Wednesday);
        var wed = s.First(x => SessionText.DayOf(x) == DayOfWeek.Wednesday);
        var h = H(p, wed, true);
        var cell = WeekPlanner.Build(s, [p], [h], Club).Single(c => c.SessionText == wed);
        Assert.False(cell.PreTicked);
        Assert.Same(h, cell.AlreadyRegistered);
    }

    [Fact]
    public void Build_FailedHistory_DoesNotCountAsRegistered()
    {
        var s = Sessions();
        var p = P(DayOfWeek.Wednesday);
        var wed = s.First(x => SessionText.DayOf(x) == DayOfWeek.Wednesday);
        var cell = WeekPlanner.Build(s, [p], [H(p, wed, false)], Club).Single(c => c.SessionText == wed);
        Assert.True(cell.PreTicked);
        Assert.Null(cell.AlreadyRegistered);
    }

    [Fact]
    public void Build_NoSessions_ReturnsEmpty() =>
        Assert.Empty(WeekPlanner.Build([], [P(DayOfWeek.Monday)], [], Club));

    [Fact]
    public void History_FromTestForm_NotRegisteredOnClubForm()
    {
        var s = Sessions();
        var p = P();
        var test = FormAddress.FormId(new Uri(FormUrls.TestForm))!;
        var sentToTest = new HistoryEntry { PersonId = p.Id, FormId = test, SessionText = s[0], Succeeded = true, At = DateTimeOffset.UtcNow };
        Assert.Null(WeekPlanner.Build(s, [p], [sentToTest], Club).Single(c => c.SessionText == s[0]).AlreadyRegistered);
        Assert.Same(sentToTest, WeekPlanner.Build(s, [p], [sentToTest], test).Single(c => c.SessionText == s[0]).AlreadyRegistered);
    }

    [Fact]
    public void History_NullFormId_TreatedAsClubForm()
    {
        var s = Sessions();
        var p = P();
        var old = H(p, s[0], true);
        Assert.Null(old.FormId);
        Assert.Same(old, WeekPlanner.Build(s, [p], [old], Club).Single(c => c.SessionText == s[0]).AlreadyRegistered);
        var test = FormAddress.FormId(new Uri(FormUrls.TestForm))!;
        Assert.Null(WeekPlanner.Build(s, [p], [old], test).Single(c => c.SessionText == s[0]).AlreadyRegistered);
    }
}
