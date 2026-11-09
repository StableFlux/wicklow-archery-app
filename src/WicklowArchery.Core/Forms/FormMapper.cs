using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WicklowArchery.Core.Forms;

/// <summary>Finds each known question by title and reports structural changes as user-facing text.</summary>
public static class FormMapper
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Url = new(@"https://\S+", RegexOptions.Compiled);

    private static string Normalise(string title) =>
        Whitespace.Replace(title.Trim(), " ").ToLowerInvariant();

    private sealed record Key(string Contains, QuestionType Type, string Label);

    private static readonly Key FullName = new("full name", QuestionType.ShortText, "Full Name");
    private static readonly Key ClubName = new("club name", QuestionType.ShortText, "Club name");
    private static readonly Key ClubNumber = new("club number", QuestionType.ShortText, "Club Number");
    private static readonly Key Venue = new("venue", QuestionType.Checkbox, "Venue options");
    private static readonly Key Payment = new("payment method", QuestionType.Radio, "Payment Method");
    private static readonly Key Loan = new("loan", QuestionType.Radio, "LOAN EQUIPMENT");
    private const string PaymentLink = "payment link";

    public static FormCheck Map(FormDefinition definition)
    {
        var problems = new List<string>();
        var matched = new HashSet<FormQuestion>();

        // The payment link block is claimed first so it is never taken as the payment question.
        var link = definition.Questions.FirstOrDefault(q => Normalise(q.Title).Contains(PaymentLink));
        if (link is not null) matched.Add(link);
        var candidates = definition.Questions.Where(q => !ReferenceEquals(q, link)).ToList();

        FormQuestion? Find(Key key, bool optional)
        {
            var hits = candidates.Where(q => Normalise(q.Title).Contains(key.Contains)).ToList();
            foreach (var h in hits) matched.Add(h);
            if (hits.Count == 0)
            {
                if (!optional) problems.Add($"Can't find the \"{key.Label}\" question on the form.");
                return null;
            }
            if (hits.Count > 1)
            {
                problems.Add($"More than one question matches \"{key.Label}\".");
                return null;
            }
            var hit = hits[0];
            if (hit.Type != key.Type || hit.EntryId is null)
            {
                problems.Add($"The \"{key.Label}\" question has changed type.");
                return null;
            }
            return hit;
        }

        var fullName = Find(FullName, false);
        var clubName = Find(ClubName, false);
        var clubNumber = Find(ClubNumber, false);
        var venue = Find(Venue, false);
        var payment = Find(Payment, false);
        var loan = Find(Loan, true);

        if (loan is { Required: true })
        {
            problems.Add("\"LOAN EQUIPMENT\" is now required.");
        }

        foreach (var q in definition.Questions)
        {
            if (q.Required && q.EntryId is not null && !matched.Contains(q))
                problems.Add($"The form has a new required question: \"{q.Title.Trim()}\".");
        }

        Uri? sumUp = null;
        var m = link?.Description is { } d ? Url.Match(d) : Match.Empty;
        if (m.Success && Uri.TryCreate(m.Value, UriKind.Absolute, out var u)) sumUp = u;

        RegistrationForm? form = null;
        if (fullName is not null && clubName is not null && clubNumber is not null && venue is not null && payment is not null)
        {
            form = new RegistrationForm(definition.ResponseUrl,
                fullName.EntryId!.Value, clubName.EntryId!.Value, clubNumber.EntryId!.Value,
                venue.EntryId!.Value, payment.EntryId!.Value, loan?.EntryId,
                venue.Options, payment.Options, loan?.Options ?? [],
                sumUp, definition.HiddenFields);
        }

        return new FormCheck(form, problems);
    }
}
