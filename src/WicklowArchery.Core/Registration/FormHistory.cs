using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;

namespace WicklowArchery.Core.Registration;

/// <summary>History belongs to one form: a send to the test form must not mark anyone registered on the club form.</summary>
public static class FormHistory
{
    /// <summary>Entries saved before the form id was recorded have none; they were all sent to the club form.</summary>
    public static string ClubFormId { get; } = FormAddress.FormId(new Uri(FormUrls.ClubForm))!;

    public static bool IsForForm(this HistoryEntry entry, string? formId) =>
        string.Equals(entry.FormId ?? ClubFormId, formId, StringComparison.Ordinal);

    /// <summary>The latest successful send of <paramref name="session"/> for this person to this form, if any.</summary>
    public static HistoryEntry? LastSuccess(IEnumerable<HistoryEntry> history, Guid personId, string session, string? formId) =>
        history
            .Where(h => h.Succeeded && h.PersonId == personId && h.IsForForm(formId)
                        && string.Equals(h.SessionText, session, StringComparison.Ordinal))
            .OrderByDescending(h => h.At)
            .FirstOrDefault();
}
