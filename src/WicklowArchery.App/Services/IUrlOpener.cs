using System.Diagnostics;

namespace WicklowArchery.App.Services;

public interface IUrlOpener
{
    void Open(Uri url);
}

/// <summary>Opens a link in the user's default browser.</summary>
public sealed class ShellUrlOpener : IUrlOpener
{
    public void Open(Uri url) =>
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
}
