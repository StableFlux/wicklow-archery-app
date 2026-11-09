using System.Net;
using System.Net.Http;

namespace WicklowArchery.Core.Net;

/// <summary>Google answered a form request, but not with 2xx. <see cref="Location"/> is the redirect target, if any.</summary>
public sealed class FormHttpStatusException(int status, Uri? location = null)
    : HttpRequestException($"Google replied with HTTP {status}.", null, (HttpStatusCode)status)
{
    public int Status { get; } = status;
    public Uri? Location { get; } = location;
}
