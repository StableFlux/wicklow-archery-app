using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WicklowArchery.App.ViewModels;

namespace WicklowArchery.App.Views;

/// <summary>The People page. Bound to a <see cref="PeopleViewModel"/>.</summary>
public partial class PeopleView : UserControl
{
    public PeopleView()
    {
        InitializeComponent();
    }

    /// <summary>Puts the cursor in Full name once the editor has been laid out.</summary>
    private void FocusName() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (DataContext is PeopleViewModel { Selected: not null } && FullNameBox.IsVisible)
                FullNameBox.Focus();
        });

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) FocusName();
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => FocusName();

    // Clicking a person moves on to editing them. Arrow keys in the list leave focus alone.
    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        for (var d = e.OriginalSource as DependencyObject; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ListBoxItem) { FocusName(); return; }
        }
    }
}
