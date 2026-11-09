using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace WicklowArchery.Core.Data;

[SupportedOSPlatform("windows")]
public sealed class DpapiProtector : IDataProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WicklowArcheryApp.v1");

    public byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
}
