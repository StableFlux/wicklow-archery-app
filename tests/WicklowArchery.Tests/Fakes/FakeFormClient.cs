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
    public string GetBody { get; set; } = "";
    public Exception? GetException { get; set; }

    public Func<int, IReadOnlyList<KeyValuePair<string, string>>, Task<HttpPostResult>> OnPost { get; set; } =
        (_, _) => Task.FromResult(new HttpPostResult(200, "Your response has been recorded."));

    public List<(Uri Url, IReadOnlyList<KeyValuePair<string, string>> Fields)> Posts { get; } = [];

    public Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        if (GetException is not null) throw GetException;
        return Task.FromResult(GetBody);
    }

    public Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        var index = Posts.Count;
        Posts.Add((url, fields));
        return OnPost(index, fields);
    }
}
