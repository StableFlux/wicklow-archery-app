namespace WicklowArchery.Tests.Core;

public class FixturesTests
{
    [Fact]
    public void Snapshots_AreCopied()
    {
        Assert.Contains("FB_PUBLIC_LOAD_DATA_", Fixtures.Read("club-form-2026-11-09.html"));
        Assert.Contains("FB_PUBLIC_LOAD_DATA_", Fixtures.Read("test-form-2026-11-09.html"));
    }
}
