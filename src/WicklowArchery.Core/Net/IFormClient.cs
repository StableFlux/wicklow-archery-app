namespace WicklowArchery.Core.Net;

public sealed record HttpPostResult(int StatusCode, string Body);

public interface IFormClient
{
    Task<string> GetAsync(Uri url, CancellationToken ct);

    Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct);
}
