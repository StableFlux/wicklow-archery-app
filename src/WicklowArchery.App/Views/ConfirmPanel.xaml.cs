using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WicklowArchery.App.Views;

public partial class ConfirmPanel : UserControl
{
    public ConfirmPanel() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(() => (SubmitButton.IsEnabled ? SubmitButton : CancelButton).Focus(), DispatcherPriority.Input);
}
