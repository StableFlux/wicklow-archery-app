using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WicklowArchery.App.Services;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;

namespace WicklowArchery.App.ViewModels;

public enum FormStatus { Checking, UpToDate, Changed, Offline }

public enum Page { Week, People, Settings }

/// <summary>The main window: loads the live form, tracks its status, and gates Complete Form.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly RegistrationService _service;
    private readonly AppDataStore _store;
    private readonly IUrlOpener _urlOpener;

    public MainViewModel(RegistrationService service, AppDataStore store, IUrlOpener urlOpener)
    {
        _service = service;
        _store = store;
        _urlOpener = urlOpener;

        var loaded = store.Load();
        Data = loaded.Data;
        notice = loaded.Notice;

        People = new PeopleViewModel(this);
        Settings = new SettingsViewModel(this);
        Week.PropertyChanged += OnWeekChanged;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsSubmitting);
        CompleteFormCommand = new RelayCommand(CompleteForm, () => CompleteFormDisabledReason is null);
    }

    public AppData Data { get; }
    public WeekViewModel Week { get; } = new();
    public PeopleViewModel People { get; }
    public SettingsViewModel Settings { get; }
    public RegistrationForm? Form { get; private set; }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CompleteFormCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(CompleteFormDisabledReason), nameof(ShowGrid), nameof(ShowOfflineNotice))]
    private FormStatus status = FormStatus.Checking;

    [ObservableProperty]
    private IReadOnlyList<string> problems = [];

    [ObservableProperty]
    private string? notice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWeek), nameof(ShowPeoplePage), nameof(ShowSettingsPage), nameof(ShowEmptyState), nameof(ShowGrid), nameof(ShowOfflineNotice), nameof(CompleteFormDisabledReason))]
    private Page currentPage = Page.Week;

    partial void OnCurrentPageChanged(Page value) => CompleteFormCommand.NotifyCanExecuteChanged();

    /// <summary>A modal panel shown over the week: the confirm panel, then the results.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentEnabled))]
    private object? overlay;

    /// <summary>False while an overlay is open: everything behind it is locked.</summary>
    public bool IsContentEnabled => Overlay is null;

    /// <summary>True from the start of a send until it has finished. The window must not close meanwhile.</summary>
    [ObservableProperty]
    private bool isSubmitting;

    partial void OnIsSubmittingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

    public bool IsTestForm => !FormUrls.IsClubForm(Data.Settings.FormUrl);

    public bool ShowWeek => CurrentPage == Page.Week;
    public bool ShowPeoplePage => CurrentPage == Page.People;
    public bool ShowSettingsPage => CurrentPage == Page.Settings;

    /// <summary>First run: nobody to register yet.</summary>
    public bool ShowEmptyState => CurrentPage == Page.Week && Data.People.Count == 0;

    public bool ShowGrid => CurrentPage == Page.Week && Data.People.Count > 0 && Form is not null;

    /// <summary>People exist but the form hasn't loaded, so there is no grid to show yet.</summary>
    public bool ShowOfflineNotice => CurrentPage == Page.Week && Data.People.Count > 0 && Form is null && Status != FormStatus.Checking;

    public string StatusText => Status switch
    {
        FormStatus.Checking => "Checking this week's form…",
        FormStatus.UpToDate => $"This week's form ✓ · {Form?.Sessions.Count ?? 0} sessions",
        FormStatus.Changed => "Form changed: Complete Form disabled",
        _ => "Can't reach the form",
    };

    public string? CompleteFormDisabledReason
    {
        get
        {
            // Ctrl+Enter is bound window-wide; only the week page can open the confirm panel.
            if (CurrentPage != Page.Week) return "Open the week page first.";
            if (Status == FormStatus.Checking) return "Checking the form…";
            if (Status == FormStatus.Offline) return "Can't reach the form.";
            if (Status == FormStatus.Changed) return "The form has changed. Click the status for details.";
            if (Data.People.Count == 0) return "Add a person first.";
            if (Form is null || Form.Sessions.Count == 0) return "No sessions on this week's form.";
            if (Week.RegistrationCount == 0) return "Tick at least one session.";
            return null;
        }
    }

    public Task InitializeAsync() => RefreshCommand.ExecuteAsync(null);

    public void SaveData() => _store.Save(Data);

    /// <summary>A name was edited: update its label in the grid, nothing else.</summary>
    public void PersonRenamed(Person person) => Week.RefreshName(person);

    /// <summary>The People page added, removed or edited someone: bring the main screen up to date.</summary>
    public void PeopleChanged(Person? changed = null)
    {
        if (Form is not null)
        {
            // Everyone else keeps their ticks; only the changed person goes back to their usual days.
            var ticks = Week.SnapshotTicks();
            Week.Load(Form, Data);
            Week.RestoreTicks(ticks, changed?.Id);
        }
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowOfflineNotice));
        OnPropertyChanged(nameof(CompleteFormDisabledReason));
        CompleteFormCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The Settings page added, renamed, removed or re-defaulted a club: the People editors pick it up, and it is saved.</summary>
    public void ClubsChanged()
    {
        People.RefreshClubs();
        try { SaveData(); }
        catch (Exception ex) { Notice = $"Couldn't save your details: {ex.Message}"; }
    }

    /// <summary>
    /// The form address was changed on the Settings page. The test-form badge follows it, the old form and its
    /// grid are dropped at once (so nothing can be sent to the previous form), and the new form is checked.
    /// </summary>
    public Task FormAddressChangedAsync()
    {
        OnPropertyChanged(nameof(IsTestForm));
        Form = null;
        Week.Clear();
        Problems = [];
        Status = FormStatus.Checking;
        NotifyFormStateChanged();
        return RefreshCommand.ExecuteAsync(null);
    }

    private const string FormMismatchMessage =
        "The form address changed since this form was checked. Nothing was sent. Wait for the new form to load, then try again.";

    /// <summary>True when <paramref name="form"/> is the form the saved address points at.</summary>
    private bool IsCurrentAddress(RegistrationForm form)
    {
        var saved = FormAddress.TryParse(Data.Settings.FormUrl);
        var savedId = saved is null ? null : FormAddress.FormId(saved);
        return savedId is not null && string.Equals(FormAddress.FormId(form.ResponseUrl), savedId, StringComparison.Ordinal);
    }

    partial void OnCurrentPageChanging(Page value)
    {
        if (CurrentPage == Page.Settings && value != Page.Settings) Settings.OnLeaving();
        if (value == Page.Settings && CurrentPage != Page.Settings) Settings.OnEntering();
    }

    public void OpenUrl(Uri url) => _urlOpener.Open(url);

    [RelayCommand]
    private void Navigate(Page page) => CurrentPage = page;

    /// <summary>First run: go to the People page with a new person ready to type into.</summary>
    [RelayCommand]
    private void AddFirstPerson()
    {
        CurrentPage = Page.People;
        People.AddCommand.Execute(null);
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    private void CompleteForm()
    {
        if (Form is not { } form || Overlay is not null || CurrentPage != Page.Week) return;
        if (!IsCurrentAddress(form))
        {
            Notice = FormMismatchMessage;
            return;
        }
        var items = ConfirmationBuilder.Build(form, Week.GetSelections(), Data.Clubs, Data.History);
        // The panel keeps the form it was built from; Submit sends to exactly that form.
        Overlay = new ConfirmViewModel(form, items, Data.History, SubmitAsync, () => Overlay = null);
    }

    private Task? _run;

    /// <summary>
    /// Sends the items one at a time, showing per-person progress. Each finished person is written to the
    /// history and saved straight away, so a crash part-way keeps what was sent. A second call while a run is
    /// in progress joins it rather than sending again. Refuses (and sends nothing) if <paramref name="form"/> is no
    /// longer the form the saved address points at.
    /// </summary>
    public Task SubmitAsync(RegistrationForm form, IReadOnlyList<ConfirmItem> items)
    {
        if (_run is { IsCompleted: false } running) return running;
        if (!IsCurrentAddress(form))
        {
            Notice = FormMismatchMessage;
            Overlay = null;
            return Task.CompletedTask;
        }
        IsSubmitting = true;
        return _run = RunAsync(form, items);
    }

    private ResultsViewModel? _results;

    private async Task RunAsync(RegistrationForm form, IReadOnlyList<ConfirmItem> items)
    {
        ResultsViewModel? results = null;
        try
        {
            var ticked = Week.GetSelections().ToDictionary(x => x.Person.Id, x => x.Sessions);
            results = new ResultsViewModel(items, form.SumUpUrl, _urlOpener.Open, () => Overlay = null);
            _results = results;
            Overlay = results;
            var formId = FormAddress.FormId(form.ResponseUrl);
            var progress = new InlineProgress<PersonResult>(r => OnUiThread(() => Record(results, r, formId)));
            try
            {
                await _service.SubmitAsync(form, items, progress, CancellationToken.None);
            }
            catch (Exception ex)
            {
                OnUiThread(() => results.FailUnfinished($"Couldn't send: {ex.Message}"));
            }
            OnUiThread(() =>
            {
                results.Finish();
                // The grid follows the form that is loaded now; if there is none, there is no grid to refresh.
                if (Form is not { } current) return;
                Week.Load(current, Data);
                // Failed or skipped people keep their ticks, so a retry is one click.
                foreach (var row in results.Rows.Where(r => r.State is PersonResultState.Failed or PersonResultState.Skipped))
                {
                    if (!ticked.TryGetValue(row.Item.Person.Id, out var sessions)) continue;
                    var gridRow = Week.Rows.FirstOrDefault(r => r.Person.Id == row.Item.Person.Id);
                    if (gridRow is null) continue;
                    foreach (var cell in gridRow.Cells.Where(c => sessions.Contains(c.SessionText)))
                        cell.IsTicked = true;
                }
            });
        }
        catch (Exception ex)
        {
            // Never leave the overlay on "Sending...": fail what is unfinished and let the user press Done.
            OnUiThread(() =>
            {
                if (results is null) Notice = $"Couldn't send: {ex.Message}";
                else { results.FailUnfinished($"Couldn't send: {ex.Message}"); results.Finish(); }
            });
        }
        finally
        {
            OnUiThread(() =>
            {
                _results = null;
                results?.Finish();
                IsSubmitting = false;
                if (Notice == CloseBlockedMessage) Notice = null;
            });
        }
    }

    /// <summary>
    /// Called when the window is asked to close. While a send is running it refuses, and says so on the
    /// results card (where the user is looking) as well as in the notice banner.
    /// </summary>
    public bool TryBlockClose()
    {
        if (!IsSubmitting) return false;
        if (_results is not null) _results.CloseBlockedMessage = CloseBlockedMessage;
        Notice = CloseBlockedMessage;
        return true;
    }

    private const string CloseBlockedMessage = "Sending in progress. Close the app when it finishes.";

    private void Record(ResultsViewModel results, PersonResult result, string? formId)
    {
        results.Apply(result);
        if (result.State is not (PersonResultState.Submitted or PersonResultState.Failed) || result.Item.Submission is null)
            return;
        foreach (var session in result.Item.Submission.Sessions)
        {
            Data.History.Add(new HistoryEntry
            {
                PersonId = result.Item.Person.Id,
                FormId = formId,
                SessionText = session,
                At = result.At,
                Succeeded = result.State == PersonResultState.Submitted,
                Error = result.Error,
            });
        }
        try { SaveData(); }
        catch (Exception ex) { Notice = $"Couldn't save your details: {ex.Message}"; }
    }

    /// <summary>The service may report from a pool thread; the view model's state belongs to the UI thread.</summary>
    private static void OnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private int _loadGeneration;

    /// <summary>
    /// Checks the form at the saved address. Only the newest check may change anything: a slow earlier check
    /// (for example of the previous form address) is discarded when it finally answers.
    /// </summary>
    private async Task LoadAsync()
    {
        var generation = ++_loadGeneration;
        var url = Data.Settings.FormUrl;
        Status = FormStatus.Checking;

        FormLoadResult? result = null;
        Exception? failure = null;
        try { result = await _service.LoadFormAsync(url, CancellationToken.None); }
        catch (Exception ex) { failure = ex; }

        if (generation != _loadGeneration || !string.Equals(url, Data.Settings.FormUrl, StringComparison.Ordinal))
            return;

        try
        {
            if (failure is not null)
            {
                Form = null;
                Problems = [$"Something went wrong while checking the form: {failure.Message}"];
                Status = FormStatus.Changed;
            }
            else if (result!.Check is { IsUsable: true, Form: { } form })
            {
                var sameForm = Form is not null
                    && FormAddress.FormId(Form.ResponseUrl) == FormAddress.FormId(form.ResponseUrl)
                    && Form.Sessions.SequenceEqual(form.Sessions);
                Form = form;
                Problems = [];
                if (!sameForm)
                    Week.Load(form, Data);
                Status = FormStatus.UpToDate;
            }
            else if (result.Check is { } check)
            {
                Form = null;
                Problems = check.Problems.Count > 0 ? check.Problems : ["The form's questions are not as expected."];
                Status = FormStatus.Changed;
            }
            else if (result.Error is null || string.Equals(result.Error, RegistrationService.NetworkErrorMessage, StringComparison.Ordinal))
            {
                Problems = [];
                Status = FormStatus.Offline;
            }
            else
            {
                Form = null;
                Problems = [result.Error];
                Status = FormStatus.Changed;
            }
        }
        catch (Exception ex)
        {
            Form = null;
            Problems = [$"Something went wrong while checking the form: {ex.Message}"];
            Status = FormStatus.Changed;
        }
        finally
        {
            if (Status == FormStatus.Checking)
                Status = FormStatus.Changed;
            NotifyFormStateChanged();
        }
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(Form));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowOfflineNotice));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CompleteFormDisabledReason));
        CompleteFormCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusChanged(FormStatus value) => CompleteFormCommand.NotifyCanExecuteChanged();

    private void OnWeekChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeekViewModel.RegistrationCount) or nameof(WeekViewModel.HasSessions))
        {
            OnPropertyChanged(nameof(CompleteFormDisabledReason));
            CompleteFormCommand.NotifyCanExecuteChanged();
        }
    }
}
