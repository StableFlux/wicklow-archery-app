using System.Net.Http;

namespace WicklowArchery.Core.Net;

/// <summary>Real HTTP client. The caller owns the HttpClient and its timeout.</summary>
public sealed class HttpFormClient(HttpClient http) : IFormClient
{
    public static string Encode(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        new FormUrlEncodedContent(fields).ReadAsStringAsync().GetAwaiter().GetResult();

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public async Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
        AddHeaders(request);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new HttpPostResult((int)response.StatusCode, body);
    }

    private static void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", "WicklowArcheryApp/1.0");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
    }
}
