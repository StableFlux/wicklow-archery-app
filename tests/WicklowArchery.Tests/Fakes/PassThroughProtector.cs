using WicklowArchery.Core.Data;

namespace WicklowArchery.Tests.Fakes;

public sealed class PassThroughProtector : IDataProtector
{
    public byte[] Protect(byte[] data) => (byte[])data.Clone();
    public byte[] Unprotect(byte[] data) => (byte[])data.Clone();
}
