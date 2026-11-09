using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;
using WicklowArchery.Tests.Fakes;
using Xunit;

namespace WicklowArchery.Tests.Core;

public class HttpFormClientTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }

    [Fact]
    public void Encode_IrishNamesRoundTrip()
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("entry.1", "Seán Ó Briain"), new("entry.2", "O'Brien & Co"), new("entry.3", "a=b+c"),
        };
        var encoded = HttpFormClient.Encode(fields);
        var decoded = encoded.Split('&').Select(p => p.Split('=')).Select(kv =>
            new KeyValuePair<string, string>(WebUtility.UrlDecode(kv[0]), WebUtility.UrlDecode(kv[1]))).ToList();
        Assert.Equal(fields, decoded);
    }

    [Fact]
    public async Task HttpClient_SendsHeadersOnGetAndPost()
    {
        var handler = new CaptureHandler();
        var client = new HttpFormClient(new HttpClient(handler));
        await client.GetAsync(new Uri("https://example.test/a"), CancellationToken.None);
        await client.PostAsync(new Uri("https://example.test/b"), [new("a", "b")], CancellationToken.None);
        Assert.Equal(2, handler.Requests.Count);
        foreach (var r in handler.Requests)
        {
            Assert.Equal("WicklowArcheryApp/1.0", r.Headers.UserAgent.ToString());
            Assert.Equal("en", r.Headers.AcceptLanguage.ToString());
        }
    }

    [Fact]
    public async Task GuardClient_PostToClubForm_Throws()
    {
        var fake = new FakeFormClient();
        var guard = new ClubFormGuardClient(fake);
        var url = new Uri(FormUrls.ClubForm.Replace("/viewform", "/formResponse"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => guard.PostAsync(url, [], CancellationToken.None));
        Assert.Equal("Debug build: submitting to the club's real form is blocked.", ex.Message);
        Assert.Empty(fake.Posts);
    }

    [Fact]
    public async Task GuardClient_PostToTestForm_PassesThrough()
    {
        var fake = new FakeFormClient();
        var guard = new ClubFormGuardClient(fake);
        var url = new Uri(FormUrls.TestForm.Replace("/viewform", "/formResponse"));
        var result = await guard.PostAsync(url, [], CancellationToken.None);
        Assert.Equal(200, result.StatusCode);
        Assert.Single(fake.Posts);
    }
}
