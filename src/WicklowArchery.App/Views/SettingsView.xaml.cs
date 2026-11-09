using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WicklowArchery.App.ViewModels;

namespace WicklowArchery.App.Views;

/// <summary>The Settings page. Bound to a <see cref="SettingsViewModel"/>.</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SettingsViewModel old) old.ClubAdded -= OnClubAdded;
        if (e.NewValue is SettingsViewModel vm) vm.ClubAdded += OnClubAdded;
    }

    /// <summary>A new club: put the cursor in its name, selected, ready to type over "New club".</summary>
    private void OnClubAdded(ClubRowViewModel row)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            ClubList.UpdateLayout();
            if (ClubList.ItemContainerGenerator.ContainerFromItem(row) is not DependencyObject container) return;
            if (FindNameBox(container) is not { } box) return;
            box.BringIntoView();
            box.Focus();
            box.SelectAll();
        });
    }

    private static TextBox? FindNameBox(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox { Tag: "ClubName" } box) return box;
            if (FindNameBox(child) is { } found) return found;
        }
        return null;
    }

    private void OnClubNameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClubRowViewModel row }) row.RevertIfEmpty();
    }

    /// <summary>Clicking away from the address box applies it, same as Enter.</summary>
    private void OnFormUrlLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.CommitFormUrlCommand.Execute(null);
    }
}
