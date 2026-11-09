using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WicklowArchery.App.ViewModels;

namespace WicklowArchery.App.Views;

public partial class ResultsPanel : UserControl
{
    private ResultsViewModel? _watched;

    public ResultsPanel() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResultsViewModel vm || ReferenceEquals(_watched, vm)) return;
        _watched = vm;
        vm.PropertyChanged += OnVmChanged;
        // While sending, focus stays inside this card; Done takes it once the run has finished.
        Dispatcher.BeginInvoke(() => (vm.IsFinished ? DoneButton : (IInputElement)this).Focus(), DispatcherPriority.Input);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_watched is null) return;
        _watched.PropertyChanged -= OnVmChanged;
        _watched = null;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ResultsViewModel.IsFinished) && _watched is { IsFinished: true })
            Dispatcher.BeginInvoke(() => DoneButton.Focus(), DispatcherPriority.Input);
    }
}
