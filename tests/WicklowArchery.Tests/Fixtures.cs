using System.IO;

namespace WicklowArchery.Tests;

public static class Fixtures
{
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}
