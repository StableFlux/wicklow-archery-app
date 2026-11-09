using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;

namespace WicklowArchery.App.ViewModels;

/// <summary>One club in the Settings list. Renaming writes through to the club and saves straight away.</summary>
public partial class ClubRowViewModel : ObservableObject
{
    private readonly SettingsViewModel _settings;

    public ClubRowViewModel(Club club, SettingsViewModel settings)
    {
        Club = club;
        _settings = settings;
        name = club.Name;
        isDefault = club.IsDefault;
    }

    public Club Club { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private bool isDefault;

    /// <summary>The inline "Remove {name}?" question is showing.</summary>
    [ObservableProperty]
    private bool isConfirmingRemove;

    /// <summary>A rule that applies to this club, shown on its row in the caution colour.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? message;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public string RemovePrompt => $"Remove {(string.IsNullOrWhiteSpace(Club.Name) ? "this club" : Club.Name.Trim())}?";

    partial void OnNameChanged(string value) => _settings.Renamed(this, value);

    /// <summary>When the box loses focus empty, put the saved name back.</summary>
    public void RevertIfEmpty()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return;
        Name = Club.Name;
    }

    /// <summary>Re-reads the club after it changed elsewhere (the default moved).</summary>
    internal void Sync()
    {
        IsDefault = Club.IsDefault;
        OnPropertyChanged(nameof(RemovePrompt));
    }
}

/// <summary>The Settings page: the clubs, and which form the app talks to.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private const string InvalidFormMessage = "That isn't a Google Form link. Paste the full docs.google.com/forms link.";

    private readonly MainViewModel _owner;

    public SettingsViewModel(MainViewModel owner)
    {
        _owner = owner;
        foreach (var club in owner.Data.Clubs)
            Clubs.Add(new ClubRowViewModel(club, this));
        formUrl = owner.Data.Settings.FormUrl;

        AddClubCommand = new RelayCommand(AddClub);
        RemoveClubCommand = new RelayCommand<ClubRowViewModel>(RemoveClub);
        AskRemoveClubCommand = new RelayCommand<ClubRowViewModel>(AskRemoveClub);
        CancelRemoveClubCommand = new RelayCommand<ClubRowViewModel>(row => { if (row is not null) row.IsConfirmingRemove = false; });
        SetDefaultClubCommand = new RelayCommand<ClubRowViewModel>(SetDefault);
        UseClubFormCommand = new AsyncRelayCommand(() => UseFormAsync(FormUrls.ClubForm));
        UseTestFormCommand = new AsyncRelayCommand(() => UseFormAsync(FormUrls.TestForm));
        CommitFormUrlCommand = new AsyncRelayCommand(CommitFormUrlAsync);
        BackCommand = new RelayCommand(() => owner.CurrentPage = Page.Week);
    }

    public ObservableCollection<ClubRowViewModel> Clubs { get; } = [];

    public IRelayCommand AddClubCommand { get; }
    public IRelayCommand<ClubRowViewModel> RemoveClubCommand { get; }
    public IRelayCommand<ClubRowViewModel> AskRemoveClubCommand { get; }
    public IRelayCommand<ClubRowViewModel> CancelRemoveClubCommand { get; }
    public IRelayCommand<ClubRowViewModel> SetDefaultClubCommand { get; }
    public IAsyncRelayCommand UseClubFormCommand { get; }
    public IAsyncRelayCommand UseTestFormCommand { get; }
    public IAsyncRelayCommand CommitFormUrlCommand { get; }
    public IRelayCommand BackCommand { get; }

    /// <summary>Raised after a club is added so the view can put the cursor in its name box.</summary>
    public event Action<ClubRowViewModel>? ClubAdded;

    /// <summary>The latest club rule message (also shown on the club's own row).</summary>
    [ObservableProperty]
    private string? clubMessage;

    [ObservableProperty]
    private string formUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFormUrlError))]
    private string? formUrlError;

    public bool HasFormUrlError => !string.IsNullOrEmpty(FormUrlError);

    private string SavedUrl => _owner.Data.Settings.FormUrl;

    public string FormStatusText =>
        FormUrls.IsClubForm(SavedUrl) ? "Club form — registrations go to the club."
        : string.Equals(SavedUrl, FormUrls.TestForm, StringComparison.Ordinal) ? "Test form — registrations go to the test copy, not the club."
        : "Another form — registrations go there, not to the club.";

    public bool IsFormStatusCaution => !FormUrls.IsClubForm(SavedUrl);

    // ---- Clubs ----

    private void SetMessage(ClubRowViewModel? row, string? text)
    {
        foreach (var other in Clubs) other.Message = null;
        if (row is not null) row.Message = text;
        ClubMessage = text;
    }

    private void Changed() => _owner.ClubsChanged();

    private void AddClub()
    {
        var club = new Club { Name = "New club", IsDefault = false };
        _owner.Data.Clubs.Add(club);
        var row = new ClubRowViewModel(club, this);
        Clubs.Add(row);
        SetMessage(null, null);
        Changed();
        ClubAdded?.Invoke(row);
    }

    internal void Renamed(ClubRowViewModel row, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SetMessage(row, "A club needs a name.");
            return;
        }
        SetMessage(null, null);
        if (row.Club.Name == value) return;
        row.Club.Name = value;
        row.Sync();
        Changed();
    }

    private string? RemoveBlockedReason(ClubRowViewModel row)
    {
        var users = _owner.Data.People.Count(p => p.ClubId == row.Club.Id);
        if (users > 0) return users == 1 ? "In use by 1 person." : $"In use by {users} people.";
        if (_owner.Data.Clubs.Count <= 1) return "You need at least one club.";
        return null;
    }

    private void AskRemoveClub(ClubRowViewModel? row)
    {
        if (row is null) return;
        foreach (var other in Clubs) other.IsConfirmingRemove = false;
        if (RemoveBlockedReason(row) is { } reason) { SetMessage(row, reason); return; }
        SetMessage(null, null);
        row.IsConfirmingRemove = true;
    }

    private void RemoveClub(ClubRowViewModel? row)
    {
        if (row is null) return;
        row.IsConfirmingRemove = false;
        if (RemoveBlockedReason(row) is { } reason) { SetMessage(row, reason); return; }

        _owner.Data.Clubs.Remove(row.Club);
        Clubs.Remove(row);
        // Exactly one default at all times.
        if (!_owner.Data.Clubs.Any(c => c.IsDefault))
            _owner.Data.Clubs[0].IsDefault = true;
        foreach (var r in Clubs) r.Sync();
        SetMessage(null, null);
        Changed();
    }

    private void SetDefault(ClubRowViewModel? row)
    {
        if (row is null) return;
        foreach (var r in Clubs)
        {
            r.Club.IsDefault = ReferenceEquals(r, row);
            r.Sync();
        }
        SetMessage(null, null);
        Changed();
    }

    // ---- Form address ----

    private async Task UseFormAsync(string url)
    {
        FormUrl = url;
        await CommitFormUrlAsync();
    }

    /// <summary>
    /// Applies what is in the address box: nothing if unchanged, an inline error if it is not a Google Form link,
    /// otherwise save and check that form. The saving happens before the first await.
    /// </summary>
    public Task CommitFormUrlAsync()
    {
        var text = (FormUrl ?? "").Trim();
        if (text == SavedUrl)
        {
            FormUrl = text;
            FormUrlError = null;
            return Task.CompletedTask;
        }
        if (FormAddress.TryParse(text) is null)
        {
            FormUrlError = InvalidFormMessage;
            return Task.CompletedTask;
        }

        FormUrl = text;
        FormUrlError = null;
        _owner.Data.Settings.FormUrl = text;
        try { _owner.SaveData(); }
        catch (Exception ex) { _owner.Notice = $"Couldn't save your details: {ex.Message}"; }
        OnPropertyChanged(nameof(FormStatusText));
        OnPropertyChanged(nameof(IsFormStatusCaution));
        return _owner.FormAddressChangedAsync();
    }

    /// <summary>Called when the page is left: apply a pending edit, or quietly drop one that is not a form link.</summary>
    internal void OnLeaving()
    {
        _ = CommitFormUrlAsync();
        if (HasFormUrlError) { FormUrl = SavedUrl; FormUrlError = null; }
        foreach (var row in Clubs) row.IsConfirmingRemove = false;
    }

    /// <summary>Called when the page is opened: show what is saved, with no stale messages.</summary>
    internal void OnEntering()
    {
        FormUrl = SavedUrl;
        FormUrlError = null;
        SetMessage(null, null);
    }
}
