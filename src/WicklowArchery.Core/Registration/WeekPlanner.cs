using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;

namespace WicklowArchery.Core.Registration;

public sealed record WeekCell(Guid PersonId, string SessionText, bool PreTicked, HistoryEntry? AlreadyRegistered);

public static class WeekPlanner
{
    /// <param name="formId">The form being shown; only history sent to this form counts as registered.</param>
    public static IReadOnlyList<WeekCell> Build(IReadOnlyList<string> sessions, IReadOnlyList<Person> people,
        IReadOnlyList<HistoryEntry> history, string? formId)
    {
        var cells = new List<WeekCell>();
        foreach (var person in people)
        {
            foreach (var session in sessions)
            {
                var done = FormHistory.LastSuccess(history, person.Id, session, formId);
                var day = Forms.SessionText.DayOf(session);
                var usual = day is not null && person.UsualDays.Contains(day.Value);
                cells.Add(new WeekCell(person.Id, session, usual && done is null, done));
            }
        }
        return cells;
    }
}
