using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Registration;

namespace WicklowArchery.App.ViewModels;

/// <summary>One person's tick for one session.</summary>
public partial class SessionCellViewModel : ObservableObject
{
    public SessionCellViewModel(Guid personId, string sessionText, bool isTicked, HistoryEntry? alreadyRegistered)
    {
        PersonId = personId;
        SessionText = sessionText;
        AlreadyRegistered = alreadyRegistered;
        this.isTicked = isTicked;
    }

    public Guid PersonId { get; }
    public string SessionText { get; }
    public HistoryEntry? AlreadyRegistered { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRepeat))]
    private bool isTicked;

    public bool IsRegistered => AlreadyRegistered is not null;

    /// <summary>Ticked although this person is already registered for the session: sending would repeat it.</summary>
    public bool IsRepeat => AlreadyRegistered is not null && IsTicked;

    public string? RegisteredToolTip => AlreadyRegistered is null
        ? null
        : $"Registered on {AlreadyRegistered.At.ToLocalTime():ddd d MMM 'at' HH:mm}. Click to send it again.";
}

/// <summary>A session column header: day in bold, the rest of the option text below.</summary>
public sealed class SessionHeaderViewModel(string fullText)
{
    public string Headline { get; } = SessionText.Split(fullText).Headline;
    public string Detail { get; } = SessionText.Split(fullText).Detail;
    public string FullText { get; } = fullText;
}

public sealed class PersonRowViewModel(Person person, IEnumerable<SessionCellViewModel> cells) : ObservableObject
{
    public Person Person { get; } = person;
    public string Name => Person.FullName;
    public ObservableCollection<SessionCellViewModel> Cells { get; } = new(cells);

    public void RefreshName() => OnPropertyChanged(nameof(Name));
}

/// <summary>The weekly grid: people as rows, this week's sessions as columns.</summary>
public partial class WeekViewModel : ObservableObject
{
    private int _peopleCount;
    private int _registrationCount;
    private bool _hasSessions;

    public ObservableCollection<SessionHeaderViewModel> Headers { get; } = [];
    public ObservableCollection<PersonRowViewModel> Rows { get; } = [];

    /// <summary>People with at least one ticked cell.</summary>
    public int PeopleCount
    {
        get => _peopleCount;
        private set => SetProperty(ref _peopleCount, value);
    }

    /// <summary>Ticked cells across everyone.</summary>
    public int RegistrationCount
    {
        get => _registrationCount;
        private set => SetProperty(ref _registrationCount, value);
    }

    public string SummaryText => RegistrationCount == 0
        ? "Nothing ticked yet"
        : $"{PeopleCount} {(PeopleCount == 1 ? "person" : "people")} · {RegistrationCount} {(RegistrationCount == 1 ? "registration" : "registrations")}";

    public bool HasSessions
    {
        get => _hasSessions;
        private set => SetProperty(ref _hasSessions, value);
    }

    /// <summary>Re-reads one person's name for display, leaving every tick alone.</summary>
    public void RefreshName(Person person) => Rows.FirstOrDefault(r => r.Person.Id == person.Id)?.RefreshName();

    /// <summary>Every cell's tick, keyed by person and session.</summary>
    public Dictionary<(Guid, string), bool> SnapshotTicks() =>
        Rows.SelectMany(r => r.Cells).ToDictionary(c => (c.PersonId, c.SessionText), c => c.IsTicked);

    /// <summary>Puts ticks back after a reload, for everyone except <paramref name="except"/> and for sessions that still exist.</summary>
    public void RestoreTicks(Dictionary<(Guid, string), bool> ticks, Guid? except)
    {
        foreach (var cell in Rows.SelectMany(r => r.Cells))
            if (cell.PersonId != except && ticks.TryGetValue((cell.PersonId, cell.SessionText), out var ticked))
                cell.IsTicked = ticked;
    }

    /// <summary>Empties the grid, e.g. while a different form is being checked.</summary>
    public void Clear()
    {
        foreach (var cell in Rows.SelectMany(r => r.Cells))
            cell.PropertyChanged -= OnCellChanged;
        Headers.Clear();
        Rows.Clear();
        HasSessions = false;
        Recount();
    }

    public void Load(RegistrationForm form, AppData data)
    {
        Clear();

        foreach (var session in form.Sessions)
            Headers.Add(new SessionHeaderViewModel(session));

        var cells = WeekPlanner.Build(form.Sessions, data.People, data.History);
        foreach (var person in data.People)
        {
            var rowCells = cells
                .Where(c => c.PersonId == person.Id)
                .Select(c => new SessionCellViewModel(c.PersonId, c.SessionText, c.PreTicked, c.AlreadyRegistered))
                .ToList();
            foreach (var cell in rowCells)
                cell.PropertyChanged += OnCellChanged;
            Rows.Add(new PersonRowViewModel(person, rowCells));
        }

        HasSessions = form.Sessions.Count > 0;
        Recount();
    }

    /// <summary>Everyone with at least one tick, with their sessions in the form's order.</summary>
    public IReadOnlyList<Selection> GetSelections() => Rows
        .Select(r => new Selection(r.Person, r.Cells.Where(c => c.IsTicked).Select(c => c.SessionText).ToList()))
        .Where(s => s.Sessions.Count > 0)
        .ToList();

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionCellViewModel.IsTicked))
            Recount();
    }

    private void Recount()
    {
        PeopleCount = Rows.Count(r => r.Cells.Any(c => c.IsTicked));
        RegistrationCount = Rows.Sum(r => r.Cells.Count(c => c.IsTicked));
        OnPropertyChanged(nameof(SummaryText));
    }
}
