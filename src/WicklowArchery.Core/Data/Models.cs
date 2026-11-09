namespace WicklowArchery.Core.Data;

public sealed class Club
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
}

public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public Guid ClubId { get; set; }
    public string ClubNumber { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string? LoanBow { get; set; }
    public List<DayOfWeek> UsualDays { get; set; } = [];
}

public sealed class HistoryEntry
{
    public Guid PersonId { get; set; }
    public string SessionText { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
}

public sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

public sealed class AppSettings
{
    public string FormUrl { get; set; } = FormUrls.ClubForm;
    public WindowPlacement? Window { get; set; }
}

public sealed class AppData
{
    public int Version { get; set; } = 1;
    public List<Club> Clubs { get; set; } = [];
    public List<Person> People { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = [];

    public static AppData CreateDefault() => new()
    {
        Clubs = [new Club { Name = "Wicklow Archery", IsDefault = true }],
        Settings = new AppSettings { FormUrl = FormUrls.ClubForm },
    };
}
