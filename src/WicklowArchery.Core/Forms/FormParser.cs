using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WicklowArchery.Core.Forms;

public sealed class FormParseException(string message) : Exception(message);

public static class FormParser
{
    private static readonly Regex LoadData = new(@"FB_PUBLIC_LOAD_DATA_ = (.*?);</script>", RegexOptions.Singleline);
    private static readonly Regex ActionAttr = new(@"action=""([^""]*/formResponse)""");
    private static readonly Regex InputTag = new(@"<input\b[^>]*>", RegexOptions.IgnoreCase);
    private static readonly Regex NameAttr = new(@"\bname=""([^""]*)""");
    private static readonly Regex ValueAttr = new(@"\bvalue=""([^""]*)""");

    public static FormDefinition Parse(string html)
    {
        var match = LoadData.Match(html);
        if (!match.Success)
            throw new FormParseException("The page does not contain form data (FB_PUBLIC_LOAD_DATA_).");

        try
        {
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var info = doc.RootElement[1];
            var title = info[8].GetString() ?? throw new FormParseException("The form has no title.");
            var questions = info[1].EnumerateArray().Select(ParseQuestion).ToList();
            return new FormDefinition(title, ReadResponseUrl(html), questions, ReadHiddenFields(html));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException
                                       or KeyNotFoundException or IndexOutOfRangeException or FormatException)
        {
            throw new FormParseException("The form data is not in the expected shape: " + ex.Message);
        }
    }

    private static FormQuestion ParseQuestion(JsonElement q)
    {
        var typeNumber = q[3].GetInt32();
        var type = Enum.IsDefined(typeof(QuestionType), typeNumber) ? (QuestionType)typeNumber : QuestionType.Unknown;

        long? entryId = null;
        var required = false;
        var options = new List<string>();
        if (q.GetArrayLength() > 4 && q[4].ValueKind == JsonValueKind.Array && q[4].GetArrayLength() > 0)
        {
            var e = q[4][0];
            entryId = e[0].GetInt64();
            required = e.GetArrayLength() > 2 && e[2].ValueKind == JsonValueKind.Number && e[2].GetInt32() == 1;
            if (e.GetArrayLength() > 1 && e[1].ValueKind == JsonValueKind.Array)
                options.AddRange(e[1].EnumerateArray().Select(o => o[0].GetString() ?? ""));
        }

        var description = q[2].ValueKind == JsonValueKind.String ? q[2].GetString() : null;
        var title = q[1].ValueKind == JsonValueKind.String ? q[1].GetString()! : "";
        return new FormQuestion(q[0].GetInt64(), title, description, type, entryId, required, options);
    }

    private static Uri ReadResponseUrl(string html)
    {
        var m = ActionAttr.Match(html);
        if (!m.Success)
            throw new FormParseException("The page has no formResponse action URL.");
        return new Uri(WebUtility.HtmlDecode(m.Groups[1].Value));
    }

    private static List<KeyValuePair<string, string>> ReadHiddenFields(string html)
    {
        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match tag in InputTag.Matches(html))
        {
            var text = tag.Value;
            if (!Regex.IsMatch(text, @"type=""hidden""", RegexOptions.IgnoreCase)) continue;
            var name = NameAttr.Match(text);
            var value = ValueAttr.Match(text);
            if (!name.Success || !value.Success) continue;
            var n = WebUtility.HtmlDecode(name.Groups[1].Value);
            if (n.EndsWith("_sentinel", StringComparison.Ordinal)) continue;
            fields.Add(new(n, WebUtility.HtmlDecode(value.Groups[1].Value)));
        }
        return fields;
    }
}
