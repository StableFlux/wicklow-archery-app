#if DEBUG
using System.IO;
using System.Reflection;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;

namespace WicklowArchery.App.Services;

/// <summary>Debug-only data for <c>--sample</c>: the test-form snapshot and four made-up people. No network.</summary>
internal static class SampleData
{
    private const string SnapshotName = "test-form-2026-11-09.html";

    /// <summary>A store in a fresh temp folder, seeded with the sample people and history, pointing at the test form.</summary>
    public static AppDataStore CreateStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WicklowArchery-sample-" + Guid.NewGuid().ToString("N"));
        var store = new AppDataStore(dir, new DpapiProtector());
        store.Save(CreateData());
        return store;
    }

    private static AppData CreateData()
    {
        var form = FormMapper.Map(FormParser.Parse(ReadSnapshot())).Form
            ?? throw new InvalidOperationException("The sample form snapshot could not be read.");

        var data = AppData.CreateDefault();
        data.Settings.FormUrl = FormUrls.TestForm;
        var club = data.Clubs[0];
        var payment = form.PaymentOptions.FirstOrDefault() ?? "";

        Person Make(int n, params DayOfWeek[] days) => new()
        {
            FullName = $"Sample Person {n}",
            ClubId = club.Id,
            ClubNumber = $"WA-00{n}",
            PaymentMethod = payment,
            UsualDays = days.ToList(),
        };

        var p1 = Make(1, DayOfWeek.Monday, DayOfWeek.Wednesday);
        var p2 = Make(2, DayOfWeek.Wednesday, DayOfWeek.Sunday);
        var p3 = Make(3);
        var p4 = Make(4, DayOfWeek.Sunday);
        // Person 4 pays as a non-member, so the results show "Pay with SumUp".
        p4.PaymentMethod = form.PaymentOptions.FirstOrDefault(o => o == "NON CLUB MEMBER PAYMENT") ?? payment;
        data.People.AddRange([p1, p2, p3, p4]);

        // Person 2 already registered for Wednesday, so that cell shows the "Registered" badge.
        var wednesday = form.Sessions.FirstOrDefault(s => SessionText.DayOf(s) == DayOfWeek.Wednesday);
        if (wednesday is not null)
        {
            data.History.Add(new HistoryEntry
            {
                PersonId = p2.Id,
                FormId = FormAddress.FormId(form.ResponseUrl),
                SessionText = wednesday,
                At = DateTimeOffset.Now.AddHours(-20),
                Succeeded = true,
            });
        }

        return data;
    }

    internal static string ReadSnapshot()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(SnapshotName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>Serves the embedded test-form snapshot and accepts every submission. Never touches the network.</summary>
internal sealed class SampleFormClient : IFormClient
{
    public Task<string> GetAsync(Uri url, CancellationToken ct) => Task.FromResult(SampleData.ReadSnapshot());

    public Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct) =>
        Task.FromResult(new HttpPostResult(200, "Your response has been recorded."));
}
#endif