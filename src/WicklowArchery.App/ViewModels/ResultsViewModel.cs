using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Registration;

namespace WicklowArchery.App.ViewModels;

public partial class ResultRowViewModel : ObservableObject
{
    public ResultRowViewModel(ConfirmItem item)
    {
        Item = item;
        Name = item.Person.FullName;
        SessionsText = string.Join(" · ", (item.Submission?.Sessions ?? []).Select(s => SessionText.Split(s).Headline));
        if (item.IsBlocked)
        {
            State = PersonResultState.Skipped;
            Error = item.BlockedReason;
        }
    }

    public ConfirmItem Item { get; }
    public string Name { get; }
    public string SessionsText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsSending))]
    private PersonResultState state = PersonResultState.Pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? error;

    public bool IsSending => State == PersonResultState.Sending;

    public string StatusText => State switch
    {
        PersonResultState.Submitted => "✔ Submitted",
        PersonResultState.Failed => $"✘ {Error}",
        PersonResultState.Skipped => $"Skipped: {Error}",
        PersonResultState.Sending => "Sending…",
        _ => "Waiting",
    };
}

/// <summary>Per-person progress and outcome of a send, with the optional "Pay with SumUp" link.</summary>
public partial class ResultsViewModel : ObservableObject
{
    private const string NonMemberPayment = "NON CLUB MEMBER PAYMENT";
    private readonly Uri? _sumUpUrl;
    private readonly Action<Uri> _openUrl;

    public ResultsViewModel(IReadOnlyList<ConfirmItem> items, Uri? sumUpUrl, Action<Uri> openUrl, Action done)
    {
        _sumUpUrl = sumUpUrl;
        _openUrl = openUrl;
        foreach (var item in items)
            Rows.Add(new ResultRowViewModel(item));
        PaySumUpCommand = new RelayCommand(PaySumUp);
        DoneCommand = new RelayCommand(done, () => IsFinished);
    }

    public ObservableCollection<ResultRowViewModel> Rows { get; } = [];
    public IRelayCommand PaySumUpCommand { get; }
    public IRelayCommand DoneCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool isFinished;

    /// <summary>Shown on the card when the user tries to close the window mid-send.</summary>
    [ObservableProperty]
    private string? closeBlockedMessage;

    public string Title => IsFinished ? "Done" : "Sending…";

    public bool ShowSumUp => _sumUpUrl is not null && Rows.Any(r =>
        r.State == PersonResultState.Submitted && r.Item.Submission?.PaymentMethod == NonMemberPayment);

    partial void OnIsFinishedChanged(bool value) => DoneCommand.NotifyCanExecuteChanged();

    public void Apply(PersonResult result)
    {
        var row = Rows.FirstOrDefault(r => ReferenceEquals(r.Item, result.Item));
        if (row is null) return;
        row.State = result.State;
        row.Error = result.Error;
        OnPropertyChanged(nameof(ShowSumUp));
    }

    /// <summary>Nothing may be left spinning: anything still waiting or sending has failed.</summary>
    public void FailUnfinished(string error)
    {
        foreach (var row in Rows.Where(r => r.State is PersonResultState.Pending or PersonResultState.Sending))
        {
            row.State = PersonResultState.Failed;
            row.Error = error;
        }
        OnPropertyChanged(nameof(ShowSumUp));
    }

    public void Finish()
    {
        IsFinished = true;
        CloseBlockedMessage = null;
    }

    private void PaySumUp()
    {
        if (_sumUpUrl is null) return;
        try { _openUrl(_sumUpUrl); }
        catch (Exception) { /* nothing useful to show; the link stays on the form */ }
    }
}
