using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WicklowArchery.App.ViewModels;

namespace WicklowArchery.App.Views;

public partial class ResultsPanel : UserControl
{
    public ResultsPanel() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResultsViewModel vm) return;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ResultsViewModel.IsFinished)) FocusDone(vm);
        };
        FocusDone(vm);
    }

    /// <summary>Done is the primary action once the run has finished.</summary>
    private void FocusDone(ResultsViewModel vm)
    {
        if (!vm.IsFinished) return;
        Dispatcher.BeginInvoke(() => DoneButton.Focus(), DispatcherPriority.Input);
    }
}
