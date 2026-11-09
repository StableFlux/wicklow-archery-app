using System;
using System.Text.RegularExpressions;

namespace WicklowArchery.Core.Forms;

/// <summary>Reads day names and splits session option texts for the grid column headers.</summary>
public static class SessionText
{
    private static readonly Regex DayWord = new(
        @"\b(mon(day)?|tue(s(day)?)?|wed(nesday)?|thu(r(s(day)?)?)?|fri(day)?|sat(urday)?|sun(day)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Returns the day of the first day word in the text, or null if there is none.</summary>
    public static DayOfWeek? DayOf(string sessionText)
    {
        var match = DayWord.Match(sessionText);
        if (!match.Success) return null;

        return match.Value.Substring(0, 3).ToLowerInvariant() switch
        {
            "mon" => DayOfWeek.Monday,
            "tue" => DayOfWeek.Tuesday,
            "wed" => DayOfWeek.Wednesday,
            "thu" => DayOfWeek.Thursday,
            "fri" => DayOfWeek.Friday,
            "sat" => DayOfWeek.Saturday,
            _ => DayOfWeek.Sunday,
        };
    }

    /// <summary>Splits at the first " - " into a headline and a detail line. Text with no separator becomes the headline.</summary>
    public static (string Headline, string Detail) Split(string sessionText)
    {
        var index = sessionText.IndexOf(" - ", StringComparison.Ordinal);
        if (index < 0) return (sessionText.Trim(), "");

        return (sessionText.Substring(0, index).Trim(), sessionText.Substring(index + 3).Trim());
    }
}
