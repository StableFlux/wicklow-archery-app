#if DEBUG
using System.IO;
using System.Reflection;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;

namespace WicklowArchery.App.Services;

/// <summary>Debug-only data for <c>--sample</c>: the test-form snapshot and four made-up people.</summary>
internal static class SampleData
{
    private const string SnapshotName = "test-form-2026-11-09.html";

    public static (RegistrationForm Form, AppData Data) Create()
    {
        var form = FormMapper.Map(FormParser.Parse(ReadSnapshot())).Form
            ?? throw new InvalidOperationException("The sample form snapshot could not be read.");

        var data = AppData.CreateDefault();
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
        data.People.AddRange([p1, p2, p3, p4]);

        // Person 2 already registered for Wednesday, so that cell shows the "Registered" badge.
        var wednesday = form.Sessions.FirstOrDefault(s => SessionText.DayOf(s) == DayOfWeek.Wednesday);
        if (wednesday is not null)
        {
            data.History.Add(new HistoryEntry
            {
                PersonId = p2.Id,
                SessionText = wednesday,
                At = DateTimeOffset.Now.AddHours(-20),
                Succeeded = true,
            });
        }

        return (form, data);
    }

    private static string ReadSnapshot()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(SnapshotName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
#endif
