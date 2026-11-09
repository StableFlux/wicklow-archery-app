using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WicklowArchery.Core.Data;

public sealed record LoadResult(AppData Data, string? Notice);

public sealed class AppDataStore
{
    private const string FileName = "data.bin";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly IDataProtector _protector;

    public AppDataStore(string directory, IDataProtector protector)
    {
        _directory = directory;
        _protector = protector;
    }

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
#if DEBUG
        "WicklowArcheryApp-Dev");
#else
        "WicklowArcheryApp");
#endif

    private string DataPath => Path.Combine(_directory, FileName);

    public LoadResult Load()
    {
        var path = DataPath;
        if (!File.Exists(path))
            return new LoadResult(AppData.CreateDefault(), null);

        try
        {
            var plain = _protector.Unprotect(File.ReadAllBytes(path));
            var data = JsonSerializer.Deserialize<AppData>(plain, JsonOptions)
                ?? throw new JsonException("Empty data file.");
            return new LoadResult(data, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var movedName = MoveAside(path);
            var notice = $"Your saved details couldn't be read on this PC, so the app has started fresh. The old file was kept as {movedName}.";
            return new LoadResult(AppData.CreateDefault(), notice);
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        var tmp = DataPath + ".tmp";
        File.WriteAllBytes(tmp, _protector.Protect(json));
        File.Move(tmp, DataPath, overwrite: true);
    }

    private string MoveAside(string path)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = $"data.unreadable-{stamp}.bin";
        for (var i = 1; File.Exists(Path.Combine(_directory, name)); i++)
            name = $"data.unreadable-{stamp}-{i}.bin";
        File.Move(path, Path.Combine(_directory, name));
        return name;
    }
}
