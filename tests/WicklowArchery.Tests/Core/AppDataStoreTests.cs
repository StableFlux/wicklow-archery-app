using System;
using System.IO;
using System.Linq;
using System.Text;
using WicklowArchery.Core.Data;
using WicklowArchery.Tests.Fakes;
using Xunit;

namespace WicklowArchery.Tests.Core;

public sealed class AppDataStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wa-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Load_NoFile_ReturnsSeed()
    {
        var result = new AppDataStore(_dir, new PassThroughProtector()).Load();
        var club = Assert.Single(result.Data.Clubs);
        Assert.Equal("Wicklow Archery", club.Name);
        Assert.True(club.IsDefault);
        Assert.Equal(FormUrls.ClubForm, result.Data.Settings.FormUrl);
        Assert.Null(result.Notice);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new AppDataStore(_dir, new PassThroughProtector());
        var data = AppData.CreateDefault();
        var person = new Person
        {
            FullName = "Seán Ó Briain",
            ClubId = data.Clubs[0].Id,
            ClubNumber = "123",
            PaymentMethod = "Cash",
            LoanBow = "Left handed adults bow",
            UsualDays = [DayOfWeek.Wednesday, DayOfWeek.Sunday],
        };
        data.People.Add(person);
        var at = new DateTimeOffset(2026, 11, 9, 18, 30, 0, TimeSpan.FromHours(1));
        data.History.Add(new HistoryEntry { PersonId = person.Id, SessionText = "Wed", At = at, Succeeded = false, Error = "x" });
        data.Settings.Window = new WindowPlacement { Left = 1, Top = 2, Width = 3, Height = 4, Maximized = true };
        store.Save(data);

        var loaded = new AppDataStore(_dir, new PassThroughProtector()).Load();
        Assert.Null(loaded.Notice);
        var p = Assert.Single(loaded.Data.People);
        Assert.Equal(person.Id, p.Id);
        Assert.Equal("Seán Ó Briain", p.FullName);
        Assert.Equal(data.Clubs[0].Id, p.ClubId);
        Assert.Equal("123", p.ClubNumber);
        Assert.Equal("Cash", p.PaymentMethod);
        Assert.Equal("Left handed adults bow", p.LoanBow);
        Assert.Equal([DayOfWeek.Wednesday, DayOfWeek.Sunday], p.UsualDays);
        var h = Assert.Single(loaded.Data.History);
        Assert.Equal(person.Id, h.PersonId);
        Assert.Equal("Wed", h.SessionText);
        Assert.Equal(at, h.At);
        Assert.False(h.Succeeded);
        Assert.Equal("x", h.Error);
        Assert.Equal(3, loaded.Data.Settings.Window!.Width);
        Assert.True(loaded.Data.Settings.Window.Maximized);
    }

    [Fact]
    public void Save_WithDpapi_FileIsNotPlainText()
    {
        var store = new AppDataStore(_dir, new DpapiProtector());
        var data = AppData.CreateDefault();
        data.People.Add(new Person { FullName = "Seán Ó Briain", ClubId = data.Clubs[0].Id });
        store.Save(data);

        var bytes = File.ReadAllBytes(Path.Combine(_dir, "data.bin"));
        Assert.DoesNotContain("Wicklow Archery", Encoding.UTF8.GetString(bytes));

        var loaded = new AppDataStore(_dir, new DpapiProtector()).Load();
        Assert.Null(loaded.Notice);
        Assert.Equal("Seán Ó Briain", Assert.Single(loaded.Data.People).FullName);
        Assert.Equal("Wicklow Archery", loaded.Data.Clubs[0].Name);
    }

    [Fact]
    public void Load_UndecryptableFile_MovesAsideAndReturnsSeedWithNotice()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "data.bin");
        var junk = new byte[256];
        new Random(42).NextBytes(junk);
        File.WriteAllBytes(path, junk);

        var result = new AppDataStore(_dir, new DpapiProtector()).Load();

        Assert.Equal("Wicklow Archery", Assert.Single(result.Data.Clubs).Name);
        Assert.NotNull(result.Notice);
        Assert.StartsWith("Your saved details couldn't be read", result.Notice);
        var moved = Directory.GetFiles(_dir, "data.unreadable-*.bin");
        var movedFile = Assert.Single(moved);
        Assert.Contains(Path.GetFileName(movedFile), result.Notice);
        Assert.False(File.Exists(path));
        Assert.Equal(junk, File.ReadAllBytes(movedFile));
    }

    [Fact]
    public void FormUrls_IsClubForm()
    {
        Assert.True(FormUrls.IsClubForm(FormUrls.ClubForm));
        Assert.False(FormUrls.IsClubForm(FormUrls.TestForm));
        Assert.False(FormUrls.IsClubForm(null!));
        Assert.False(FormUrls.IsClubForm(""));
    }
}
