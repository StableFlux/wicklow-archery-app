using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WicklowArchery.App.Services;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
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

        Week.PropertyChanged += OnWeekChanged;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        CompleteFormCommand = new RelayCommand(CompleteForm, () => CompleteFormDisabledReason is null);
    }

    public AppData Data { get; }
    public WeekViewModel Week { get; } = new();
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
    [NotifyPropertyChangedFor(nameof(ShowWeek), nameof(ShowPeoplePage), nameof(ShowSettingsPage), nameof(ShowEmptyState), nameof(ShowGrid), nameof(ShowOfflineNotice))]
    private Page currentPage = Page.Week;

    /// <summary>A modal panel shown over the week (the confirm panel arrives in a later step).</summary>
    [ObservableProperty]
    private object? overlay;

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

    public void OpenUrl(Uri url) => _urlOpener.Open(url);

    [RelayCommand]
    private void Navigate(Page page) => CurrentPage = page;

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    private void CompleteForm()
    {
        if (Form is null || Overlay is not null) return;
        var items = ConfirmationBuilder.Build(Form, Week.GetSelections(), Data.Clubs, Data.History);
        Overlay = new ConfirmViewModel(items, Data.History, SubmitAsync, () => Overlay = null);
    }

    private Task? _run;

    /// <summary>
    /// Sends the items one at a time, showing per-person progress. Each finished person is written to the
    /// history and saved straight away, so a crash part-way keeps what was sent. A second call while a run is
    /// in progress joins it rather than sending again.
    /// </summary>
    public Task SubmitAsync(IReadOnlyList<ConfirmItem> items)
    {
        if (_run is { IsCompleted: false } running) return running;
        return _run = RunAsync(items);
    }

    private async Task RunAsync(IReadOnlyList<ConfirmItem> items)
    {
        var form = Form!;
        var results = new ResultsViewModel(items, form.SumUpUrl, _urlOpener.Open, () => Overlay = null);
        Overlay = results;
        var progress = new InlineProgress<PersonResult>(r => OnUiThread(() => Record(results, r)));
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
            Week.Load(form, Data);
        });
    }

    private void Record(ResultsViewModel results, PersonResult result)
    {
        results.Apply(result);
        if (result.State is not (PersonResultState.Submitted or PersonResultState.Failed) || result.Item.Submission is null)
            return;
        foreach (var session in result.Item.Submission.Sessions)
        {
            Data.History.Add(new HistoryEntry
            {
                PersonId = result.Item.Person.Id,
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

    private async Task LoadAsync()
    {
        Status = FormStatus.Checking;
        try
        {
            var result = await _service.LoadFormAsync(Data.Settings.FormUrl, CancellationToken.None);

            if (result.Check is { IsUsable: true, Form: { } form })
            {
                var sameSessions = Form is not null && Form.Sessions.SequenceEqual(form.Sessions);
                Form = form;
                Problems = [];
                if (!sameSessions)
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
            OnPropertyChanged(nameof(Form));
            OnPropertyChanged(nameof(ShowGrid));
            OnPropertyChanged(nameof(ShowOfflineNotice));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CompleteFormDisabledReason));
            CompleteFormCommand.NotifyCanExecuteChanged();
        }
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
