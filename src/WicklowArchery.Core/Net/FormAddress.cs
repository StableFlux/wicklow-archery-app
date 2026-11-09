namespace WicklowArchery.Core.Net;

/// <summary>The only addresses the app will talk to: https://docs.google.com/forms/d/e/&lt;id&gt;/...</summary>
public static class FormAddress
{
    private const string Prefix = "/forms/d/e/";

    public static bool IsAllowed(Uri url) =>
        url.IsAbsoluteUri
        && url.Scheme == Uri.UriSchemeHttps
        && string.Equals(url.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase)
        && url.IsDefaultPort
        && url.UserInfo.Length == 0;

    public static string? FormId(Uri url)
    {
        if (!url.IsAbsoluteUri) return null;
        var path = url.AbsolutePath;
        if (!path.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var rest = path[Prefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0) return null;
        return rest[..slash];
    }

    public static Uri? TryParse(string? text) =>
        Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var u) && IsAllowed(u) && FormId(u) is not null ? u : null;
}

