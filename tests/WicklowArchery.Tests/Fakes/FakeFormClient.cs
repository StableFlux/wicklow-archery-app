using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WicklowArchery.Core.Net;

namespace WicklowArchery.Tests.Fakes;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class FakeFormClient : IFormClient
{
    private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _gates = new(StringComparer.Ordinal);

    /// <summary>The page returned for any form without its own body (see <see cref="SetGetBody"/>).</summary>
    public string GetBody { get; set; } = "";
    public Exception? GetException { get; set; }

    public Func<int, IReadOnlyList<KeyValuePair<string, string>>, Task<HttpPostResult>> OnPost { get; set; } =
        (_, _) => Task.FromResult(new HttpPostResult(200, "Your response has been recorded."));

    public List<(Uri Url, IReadOnlyList<KeyValuePair<string, string>> Fields)> Posts { get; } = [];

    public List<Uri> Gets { get; } = [];

    /// <summary>The page returned for GETs of this form (matched by form id).</summary>
    public void SetGetBody(string formUrl, string body) => _bodies[Key(formUrl)] = body;

    /// <summary>GETs of this form (matched by form id) wait for <paramref name="gate"/> before replying.</summary>
    public void GateGet(string formUrl, Task gate) => _gates[Key(formUrl)] = gate;

    private static string Key(Uri url) => FormAddress.FormId(url) ?? url.ToString();
    private static string Key(string url) => Key(new Uri(url));

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        Gets.Add(url);
        var key = Key(url);
        if (_gates.TryGetValue(key, out var gate)) await gate;
        if (GetException is not null) throw GetException;
        return _bodies.TryGetValue(key, out var body) ? body : GetBody;
    }

    public Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        var index = Posts.Count;
        Posts.Add((url, fields));
        return OnPost(index, fields);
    }
}
