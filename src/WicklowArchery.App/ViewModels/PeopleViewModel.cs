using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WicklowArchery.Core.Data;

namespace WicklowArchery.App.ViewModels;

/// <summary>One day-of-week pill in the editor.</summary>
public enum EditKind { Other, Name, Days }

public partial class DayChipViewModel(DayOfWeek day, string label, bool isSelected) : ObservableObject
{
    public DayOfWeek Day { get; } = day;
    public string Label { get; } = label;

    [ObservableProperty]
    private bool isSelected = isSelected;
}

/// <summary>
/// Edits one <see cref="Core.Data.Person"/>. Every change is written to the person and saved straight away.
/// </summary>
public partial class PersonEditorViewModel : ObservableObject
{
    private static readonly DayOfWeek[] WeekOrder =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private static readonly string[] DayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    private readonly MainViewModel _owner;
    private bool _refreshing;

    public PersonEditorViewModel(Person person, MainViewModel owner)
    {
        Person = person;
        _owner = owner;
        fullName = person.FullName;
        clubNumber = person.ClubNumber;
        club = owner.Data.Clubs.FirstOrDefault(c => c.Id == person.ClubId);

        var chips = new List<DayChipViewModel>();
        for (var i = 0; i < WeekOrder.Length; i++)
        {
            var chip = new DayChipViewModel(WeekOrder[i], DayLabels[i], person.UsualDays.Contains(WeekOrder[i]));
            chip.PropertyChanged += OnChipChanged;
            chips.Add(chip);
        }
        Days = chips;

        BuildOptions();
    }

    public Person Person { get; }

    /// <summary>Raised after a change has been written and saved. Arguments: this editor, what changed, and whether the save succeeded.</summary>
    public event Action<PersonEditorViewModel, EditKind, bool>? Edited;

    /// <summary>A fresh copy each time, so the drop-down notices when the clubs change.</summary>
    public IReadOnlyList<Club> Clubs => _owner.Data.Clubs.ToList();
    public IReadOnlyList<DayChipViewModel> Days { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListName), nameof(IsNameEmpty), nameof(NameHint), nameof(RemovePrompt))]
    private string fullName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListDetail))]
    private Club? club;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListDetail), nameof(NumberHint))]
    private string clubNumber;

    /// <summary>The inline "Remove {name}?" question is showing.</summary>
    [ObservableProperty]
    private bool isConfirmingRemove;

    partial void OnFullNameChanged(string value)
    {
        Person.FullName = value;
        Persist(EditKind.Name);
    }

    partial void OnClubChanged(Club? value)
    {
        if (value is null || _refreshing) return;
        Person.ClubId = value.Id;
        Persist(EditKind.Other);
    }

    partial void OnClubNumberChanged(string value)
    {
        Person.ClubNumber = value;
        Persist(EditKind.Other);
    }

    // ---- Payment and loan options (from the loaded form, plus the person's saved value if it is not there) ----

    private IReadOnlyList<string> _paymentOptions = [];
    private IReadOnlyList<string?> _loanOptions = [null];

    public IReadOnlyList<string> PaymentOptions => _paymentOptions;

    /// <summary>[0] is null, shown as "No loan bow".</summary>
    public IReadOnlyList<string?> LoanOptions => _loanOptions;

    /// <summary>What the loan drop-down shows: <see cref="LoanOptions"/> with the null entry as "No loan bow".</summary>
    public IReadOnlyList<string> LoanLabels => _loanOptions.Select(o => o ?? "No loan bow").ToList();

    public bool PaymentNotOnForm { get; private set; }
    public bool LoanNotOnForm { get; private set; }

    public string? PaymentMethod
    {
        get => string.IsNullOrEmpty(Person.PaymentMethod) ? null : Person.PaymentMethod;
        set
        {
            // A drop-down clearing its selection while the list is rebuilt is not a choice.
            if (value is null || _refreshing || value == Person.PaymentMethod) return;
            Person.PaymentMethod = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PaymentHint));
            RefreshWarnings();
            Persist(EditKind.Other);
        }
    }

    public string? LoanBow => Person.LoanBow;

    public int LoanIndex
    {
        get
        {
            var i = _loanOptions.ToList().IndexOf(string.IsNullOrEmpty(Person.LoanBow) ? null : Person.LoanBow);
            return i < 0 ? 0 : i;
        }
        set
        {
            if (value < 0 || value >= _loanOptions.Count || _refreshing) return;
            var chosen = _loanOptions[value];
            if (chosen == Person.LoanBow) return;
            Person.LoanBow = chosen;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoanBow));
            RefreshWarnings();
            Persist(EditKind.Other);
        }
    }

    /// <summary>Clubs were added, renamed or removed: rebuild the club list and keep this person's club selected.</summary>
    public void RefreshClubs()
    {
        var current = Club;
        _refreshing = true;
        try
        {
            OnPropertyChanged(nameof(Clubs));
            // The drop-down clears its selection while it is rebuilt; put it back (and drop it if the club is gone).
            Club = _owner.Data.Clubs.FirstOrDefault(c => c.Id == Person.ClubId) ?? current;
        }
        finally { _refreshing = false; }
        OnPropertyChanged(nameof(ListDetail));
    }

    /// <summary>Rebuilds the option lists, e.g. when the form finishes loading.</summary>
    public void RefreshOptions()
    {
        _refreshing = true;
        try { BuildOptions(); }
        finally { _refreshing = false; }
        OnPropertyChanged(nameof(PaymentOptions));
        OnPropertyChanged(nameof(LoanOptions));
        OnPropertyChanged(nameof(LoanLabels));
        OnPropertyChanged(nameof(PaymentMethod));
        OnPropertyChanged(nameof(LoanIndex));
        RefreshWarnings();
    }

    private void BuildOptions()
    {
        var form = _owner.Form;

        var payment = new List<string>(form?.PaymentOptions ?? []);
        var savedPayment = Person.PaymentMethod;
        if (!string.IsNullOrEmpty(savedPayment) && !payment.Contains(savedPayment))
            payment.Insert(0, savedPayment);
        _paymentOptions = payment;

        var loan = new List<string?> { null };
        loan.AddRange(form?.LoanOptions ?? []);
        var savedLoan = Person.LoanBow;
        if (!string.IsNullOrEmpty(savedLoan) && !loan.Contains(savedLoan))
            loan.Insert(1, savedLoan);
        _loanOptions = loan;

        PaymentNotOnForm = form is not null && !string.IsNullOrEmpty(savedPayment) && !form.PaymentOptions.Contains(savedPayment);
        LoanNotOnForm = form is not null && !string.IsNullOrEmpty(savedLoan) && !form.LoanOptions.Contains(savedLoan);
    }

    private void RefreshWarnings()
    {
        var form = _owner.Form;
        var payment = Person.PaymentMethod;
        var loan = Person.LoanBow;
        PaymentNotOnForm = form is not null && !string.IsNullOrEmpty(payment) && !form.PaymentOptions.Contains(payment);
        LoanNotOnForm = form is not null && !string.IsNullOrEmpty(loan) && !form.LoanOptions.Contains(loan);
        OnPropertyChanged(nameof(PaymentNotOnForm));
        OnPropertyChanged(nameof(LoanNotOnForm));
    }

    // ---- Hints and list text ----

    public string? NameHint => string.IsNullOrWhiteSpace(FullName) ? "Needed for the form" : null;
    public string? NumberHint => string.IsNullOrWhiteSpace(ClubNumber) ? "Needed for the form" : null;
    public string? PaymentHint => string.IsNullOrEmpty(Person.PaymentMethod) ? "Pick how this person pays" : null;

    public bool IsNameEmpty => string.IsNullOrWhiteSpace(FullName);
    public string ListName => IsNameEmpty ? "New person" : FullName;

    public string ListDetail => string.Join(" · ",
        new[] { Club?.Name, ClubNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string RemovePrompt => $"Remove {(IsNameEmpty ? "this person" : FullName.Trim())}?";

    // ---- Days ----

    private void OnChipChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DayChipViewModel.IsSelected)) return;
        Person.UsualDays = Days.Where(d => d.IsSelected).Select(d => d.Day).ToList();
        Persist(EditKind.Days);
    }

    private void Persist(EditKind kind)
    {
        var saved = true;
        try { _owner.SaveData(); }
        catch (Exception ex) { saved = false; _owner.Notice = $"Couldn't save your details: {ex.Message}"; }
        Edited?.Invoke(this, kind, saved);
    }
}

/// <summary>The People page: the list of people and the editor for the selected one.</summary>
public partial class PeopleViewModel : ObservableObject
{
    private readonly MainViewModel _owner;
    private CancellationTokenSource? _savedTimer;

    public PeopleViewModel(MainViewModel owner)
    {
        _owner = owner;
        foreach (var person in owner.Data.People)
            People.Add(Wrap(person));
        selected = People.FirstOrDefault();

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        AskRemoveCommand = new RelayCommand(() => { if (Selected is not null) Selected.IsConfirmingRemove = true; }, () => Selected is not null);
        CancelRemoveCommand = new RelayCommand(() => { if (Selected is not null) Selected.IsConfirmingRemove = false; });
        BackCommand = new RelayCommand(() => owner.CurrentPage = Page.Week);

        owner.PropertyChanged += OnOwnerChanged;
    }

    public ObservableCollection<PersonEditorViewModel> People { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private PersonEditorViewModel? selected;

    /// <summary>"Saved" is showing; it hides again after two seconds.</summary>
    [ObservableProperty]
    private bool showSaved;

    public bool HasSelection => Selected is not null;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand AskRemoveCommand { get; }
    public IRelayCommand CancelRemoveCommand { get; }
    public IRelayCommand BackCommand { get; }

    partial void OnSelectedChanging(PersonEditorViewModel? value)
    {
        if (Selected is not null) Selected.IsConfirmingRemove = false;
    }

    partial void OnSelectedChanged(PersonEditorViewModel? value)
    {
        RemoveCommand.NotifyCanExecuteChanged();
        AskRemoveCommand.NotifyCanExecuteChanged();
    }

    private PersonEditorViewModel Wrap(Person person)
    {
        var editor = new PersonEditorViewModel(person, _owner);
        editor.Edited += OnEdited;
        return editor;
    }

    private void Add()
    {
        var defaultClub = _owner.Data.Clubs.FirstOrDefault(c => c.IsDefault) ?? _owner.Data.Clubs.FirstOrDefault();
        var person = new Person { ClubId = defaultClub?.Id ?? Guid.Empty, PaymentMethod = "" };
        _owner.Data.People.Add(person);
        var editor = Wrap(person);
        People.Add(editor);
        Selected = editor;
        SaveNow();
        _owner.PeopleChanged();
    }

    private void Remove()
    {
        if (Selected is not { } editor) return;
        var index = People.IndexOf(editor);
        People.Remove(editor);
        _owner.Data.People.Remove(editor.Person);
        // History entries stay: they only record what was sent.
        Selected = People.Count == 0 ? null : People[Math.Min(index, People.Count - 1)];
        SaveNow();
        _owner.PeopleChanged();
    }

    private void SaveNow()
    {
        try { _owner.SaveData(); }
        catch (Exception ex) { _owner.Notice = $"Couldn't save your details: {ex.Message}"; return; }
        MarkSaved();
    }

    private void OnEdited(PersonEditorViewModel editor, EditKind kind, bool saved)
    {
        if (saved) MarkSaved();
        // A new name only changes the label in the grid; usual days change the pre-ticks, so the week reloads.
        if (kind == EditKind.Name) _owner.PersonRenamed(editor.Person);
        else if (kind == EditKind.Days) _owner.PeopleChanged(editor.Person);
    }

    public void RefreshClubs()
    {
        foreach (var editor in People) editor.RefreshClubs();
    }

    private void OnOwnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The form may finish loading after the editors were made.
        if (e.PropertyName is nameof(MainViewModel.Form) or nameof(MainViewModel.CurrentPage))
            foreach (var editor in People) editor.RefreshOptions();
        // Leaving the page mid-question cancels it.
        if (e.PropertyName == nameof(MainViewModel.CurrentPage) && _owner.CurrentPage != Page.People)
            foreach (var editor in People) editor.IsConfirmingRemove = false;
    }

    private void MarkSaved()
    {
        _savedTimer?.Cancel();
        var timer = _savedTimer = new CancellationTokenSource();
        ShowSaved = true;
        _ = HideSavedLaterAsync(timer.Token);
    }

    private async Task HideSavedLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            ShowSaved = false;
        }
        catch (OperationCanceledException) { }
    }
}
