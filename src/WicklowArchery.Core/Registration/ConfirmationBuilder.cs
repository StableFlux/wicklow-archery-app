using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;

namespace WicklowArchery.Core.Registration;

public sealed record Selection(Person Person, IReadOnlyList<string> Sessions);

public sealed record ConfirmItem(Person Person, PersonSubmission? Submission, IReadOnlyList<string> RepeatSessions, string? BlockedReason)
{
    public bool IsBlocked => BlockedReason is not null;
}

public static class ConfirmationBuilder
{
    public static IReadOnlyList<ConfirmItem> Build(RegistrationForm form, IReadOnlyList<Selection> selections,
        IReadOnlyList<Club> clubs, IReadOnlyList<HistoryEntry> history)
    {
        var formId = FormAddress.FormId(form.ResponseUrl);
        var items = new List<ConfirmItem>();
        foreach (var sel in selections)
        {
            if (sel.Sessions.Count == 0) continue;
            var person = sel.Person;
            var repeats = sel.Sessions.Where(s => FormHistory.LastSuccess(history, person.Id, s, formId) is not null).ToList();
            var club = clubs.FirstOrDefault(c => c.Id == person.ClubId);

            string? blocked = null;
            if (string.IsNullOrWhiteSpace(person.FullName)) blocked = "Missing full name.";
            else if (club is null) blocked = "No club set.";
            else if (string.IsNullOrWhiteSpace(person.ClubNumber)) blocked = "Missing club number.";
            else if (string.IsNullOrEmpty(person.PaymentMethod) || !form.PaymentOptions.Contains(person.PaymentMethod, StringComparer.Ordinal))
                blocked = $"Payment method \"{person.PaymentMethod}\" is no longer on the form. Edit this person to pick one.";
            else if (!string.IsNullOrEmpty(person.LoanBow) && !form.LoanOptions.Contains(person.LoanBow, StringComparer.Ordinal))
                blocked = $"Loan bow \"{person.LoanBow}\" is no longer on the form. Edit this person to change it.";

            var submission = blocked is null
                ? new PersonSubmission(person.Id, person.FullName, club!.Name, person.ClubNumber, sel.Sessions,
                    person.PaymentMethod ?? "", string.IsNullOrEmpty(person.LoanBow) ? null : person.LoanBow)
                : null;
            items.Add(new ConfirmItem(person, submission, repeats, blocked));
        }
        return items;
    }
}
