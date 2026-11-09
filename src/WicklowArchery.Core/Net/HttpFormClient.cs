using System.Net.Http;

namespace WicklowArchery.Core.Net;

/// <summary>Real HTTP client: only Google Forms addresses, no redirects followed, 30-second timeout.</summary>
public sealed class HttpFormClient : IFormClient
{
    private readonly HttpClient http;

    internal HttpFormClient(HttpClient http) => this.http = http;

    public static HttpFormClient Create() => new(CreateHttpClient());

    public static string Encode(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        new FormUrlEncodedContent(fields).ReadAsStringAsync().GetAwaiter().GetResult();

    internal static HttpClientHandler CreateHandler() => new() { AllowAutoRedirect = false };

    internal static HttpClient CreateHttpClient() =>
        new(CreateHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    private static void EnsureAllowed(Uri url)
    {
        if (!FormAddress.IsAllowed(url))
            throw new InvalidOperationException("Blocked request to a non-Google address.");
    }

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        EnsureAllowed(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        // Anything but 2xx fails closed; the status says why (e.g. 404 gone, 302 to a sign-in page).
        if (!response.IsSuccessStatusCode)
            throw new FormHttpStatusException((int)response.StatusCode, response.Headers.Location);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public async Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        EnsureAllowed(url);
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
