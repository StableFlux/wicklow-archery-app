using System;
using System.Collections.Generic;
using System.Linq;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Registration;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class WeekPlannerTests
{
    private static IReadOnlyList<string> Sessions() =>
        FormMapper.Map(FormParser.Parse(Fixtures.Read("club-form-2026-11-09.html"))).Form!.Sessions;

    private static Person P(params DayOfWeek[] days) => new() { FullName = "A", UsualDays = days.ToList() };

    private static HistoryEntry H(Person p, string s, bool ok) =>
        new() { PersonId = p.Id, SessionText = s, Succeeded = ok, At = DateTimeOffset.UtcNow };

    [Fact]
    public void Build_UsualWednesday_PreTicksWednesdayOnly()
    {
        var s = Sessions();
        var cells = WeekPlanner.Build(s, [P(DayOfWeek.Wednesday)], []);
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
        var cell = WeekPlanner.Build(s, [p], [newer, older]).Single(c => c.SessionText == wed);
        Assert.Same(newer, cell.AlreadyRegistered);
        cell = WeekPlanner.Build(s, [p], [older, newer]).Single(c => c.SessionText == wed);
        Assert.Same(newer, cell.AlreadyRegistered);
    }

    [Fact]
    public void Build_SessionWithoutDayName_NeverPreTicked()
    {
        var cells = WeekPlanner.Build(["Special event"], [P(Enum.GetValues<DayOfWeek>())], []);
        Assert.False(Assert.Single(cells).PreTicked);
    }

    [Fact]
    public void Build_AlreadyRegistered_MarkedAndNotPreTicked()
    {
        var s = Sessions();
        var p = P(DayOfWeek.Wednesday);
        var wed = s.First(x => SessionText.DayOf(x) == DayOfWeek.Wednesday);
        var h = H(p, wed, true);
        var cell = WeekPlanner.Build(s, [p], [h]).Single(c => c.SessionText == wed);
        Assert.False(cell.PreTicked);
        Assert.Same(h, cell.AlreadyRegistered);
    }

    [Fact]
    public void Build_FailedHistory_DoesNotCountAsRegistered()
    {
        var s = Sessions();
        var p = P(DayOfWeek.Wednesday);
        var wed = s.First(x => SessionText.DayOf(x) == DayOfWeek.Wednesday);
        var cell = WeekPlanner.Build(s, [p], [H(p, wed, false)]).Single(c => c.SessionText == wed);
        Assert.True(cell.PreTicked);
        Assert.Null(cell.AlreadyRegistered);
    }

    [Fact]
    public void Build_NoSessions_ReturnsEmpty() =>
        Assert.Empty(WeekPlanner.Build([], [P(DayOfWeek.Monday)], []));
}
