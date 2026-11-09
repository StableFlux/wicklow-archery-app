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
    private const string OfflinePrefix = "Couldn't reach the form";

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

    // The confirm panel arrives in a later build step.
    private void CompleteForm() { }

    private async Task LoadAsync()
    {
        Status = FormStatus.Checking;
        FormLoadResult result;
        try
        {
            result = await _service.LoadFormAsync(Data.Settings.FormUrl, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new FormLoadResult(null, OfflinePrefix + ".");
        }

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
        else
        {
            var error = result.Error ?? OfflinePrefix + ".";
            if (error.StartsWith(OfflinePrefix, StringComparison.Ordinal))
            {
                Problems = [];
                Status = FormStatus.Offline;
            }
            else
            {
                Form = null;
                Problems = [error];
                Status = FormStatus.Changed;
            }
        }

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
