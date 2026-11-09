using System;
using System.Collections.Generic;
using System.Globalization;

namespace WicklowArchery.Core.Forms;

public sealed record PersonSubmission(Guid PersonId, string FullName, string ClubName, string ClubNumber,
    IReadOnlyList<string> Sessions, string PaymentMethod, string? LoanBow);

public static class PayloadBuilder
{
    public static IReadOnlyList<KeyValuePair<string, string>> Build(RegistrationForm form, PersonSubmission submission)
    {
        var pairs = new List<KeyValuePair<string, string>>(form.HiddenFields);

        void Add(string key, string value) => pairs.Add(new KeyValuePair<string, string>(key, value));
        static string Key(long id) => "entry." + id.ToString(CultureInfo.InvariantCulture);

        Add(Key(form.FullNameEntry), submission.FullName.Trim());
        Add(Key(form.ClubNameEntry), submission.ClubName.Trim());
        Add(Key(form.ClubNumberEntry), submission.ClubNumber.Trim());

        foreach (var session in submission.Sessions)
            Add(Key(form.VenueEntry), session);
        Add(Key(form.VenueEntry) + "_sentinel", "");

        Add(Key(form.PaymentEntry), submission.PaymentMethod);
        Add(Key(form.PaymentEntry) + "_sentinel", "");

        if (form.LoanEntry is long loan)
        {
            if (!string.IsNullOrEmpty(submission.LoanBow))
                Add(Key(loan), submission.LoanBow);
            Add(Key(loan) + "_sentinel", "");
        }

        return pairs;
    }
}
