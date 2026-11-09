using System;
using System.Collections.Generic;

namespace WicklowArchery.Core.Forms;

/// <summary>The club form as the app understands it: entry ids and option lists found by question title.</summary>
public sealed record RegistrationForm(Uri ResponseUrl,
    long FullNameEntry, long ClubNameEntry, long ClubNumberEntry, long VenueEntry, long PaymentEntry, long? LoanEntry,
    IReadOnlyList<string> Sessions, IReadOnlyList<string> PaymentOptions, IReadOnlyList<string> LoanOptions,
    Uri? SumUpUrl, IReadOnlyList<KeyValuePair<string, string>> HiddenFields);

/// <summary>Result of checking the form. Usable only when the form was mapped and nothing was reported.</summary>
public sealed record FormCheck(RegistrationForm? Form, IReadOnlyList<string> Problems)
{
    public bool IsUsable => Form is not null && Problems.Count == 0;
}
