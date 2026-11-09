using WicklowArchery.Core.Data;

namespace WicklowArchery.Core.Net;

/// <summary>Debug safety net: refuses to POST to the club's real form.</summary>
public sealed class ClubFormGuardClient(IFormClient inner) : IFormClient
{
    public Task<string> GetAsync(Uri url, CancellationToken ct) => inner.GetAsync(url, ct);

    public Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        if (FormUrls.IsClubForm(url.ToString()))
            throw new InvalidOperationException("Debug build: submitting to the club's real form is blocked.");
        return inner.PostAsync(url, fields, ct);
    }
}
