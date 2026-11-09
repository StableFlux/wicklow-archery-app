using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Forms;
using WicklowArchery.Core.Registration;

namespace WicklowArchery.App.ViewModels;

/// <summary>One session line on the confirm panel; <see cref="RepeatText"/> is set when it was already sent successfully.</summary>
public sealed record ConfirmSessionLine(string Headline, string? RepeatText);

public sealed class ConfirmPersonViewModel
{
    public ConfirmPersonViewModel(ConfirmItem item, IReadOnlyList<HistoryEntry> history)
    {
        Name = item.Person.FullName;
        BlockedReason = item.BlockedReason;
        var sessions = item.Submission?.Sessions ?? [];
        Sessions = sessions.Select(s => new ConfirmSessionLine(SessionText.Split(s).Headline, RepeatTextFor(item, s, history))).ToList();
    }

    public string Name { get; }
    public IReadOnlyList<ConfirmSessionLine> Sessions { get; }
    public string? BlockedReason { get; }
    public bool IsBlocked => BlockedReason is not null;

    private static string? RepeatTextFor(ConfirmItem item, string session, IReadOnlyList<HistoryEntry> history)
    {
        if (!item.RepeatSessions.Contains(session)) return null;
        var last = history
            .Where(h => h.Succeeded && h.PersonId == item.Person.Id && string.Equals(h.SessionText, session, StringComparison.Ordinal))
            .OrderByDescending(h => h.At)
            .FirstOrDefault();
        var date = last is null ? "earlier" : last.At.ToLocalTime().ToString("ddd d MMM", CultureInfo.CurrentCulture);
        return $"Already registered on {date}; will be sent again";
    }
}

/// <summary>"Ready to send": who is about to be registered for what, before anything leaves the app.</summary>
public partial class ConfirmViewModel : ObservableObject
{
    public ConfirmViewModel(RegistrationForm form, IReadOnlyList<ConfirmItem> items, IReadOnlyList<HistoryEntry> history,
        Func<RegistrationForm, IReadOnlyList<ConfirmItem>, Task> submit, Action cancel)
    {
        Form = form;
        Items = items;
        People = items.Select(i => new ConfirmPersonViewModel(i, history)).ToList();
        SendableCount = items.Count(i => !i.IsBlocked);
        SubmitCommand = new AsyncRelayCommand(() => submit(Form, Items), () => SendableCount > 0);
        CancelCommand = new RelayCommand(cancel, () => !SubmitCommand.IsRunning);
        SubmitCommand.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
                CancelCommand.NotifyCanExecuteChanged();
        };
    }

    public string Title => "Ready to send";

    /// <summary>The form this panel was built from; Submit sends to exactly this form.</summary>
    public RegistrationForm Form { get; }
    public IReadOnlyList<ConfirmItem> Items { get; }
    public IReadOnlyList<ConfirmPersonViewModel> People { get; }
    public int SendableCount { get; }
    public string SubmitText => $"Submit ({SendableCount})";
    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand CancelCommand { get; }
}
