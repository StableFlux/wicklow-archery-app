using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WicklowArchery.App.Views;

/// <summary>The weekly grid. Bound to a <see cref="ViewModels.WeekViewModel"/>.</summary>
public partial class WeekView : UserControl
{
    public WeekView()
    {
        InitializeComponent();
    }

    // Headers follow the cells sideways; names follow them up and down.
    private void OnCellScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        HeaderScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        // Give the names the same scroll range as the cells, whose viewport is shorter by
        // their bottom padding and horizontal scroll bar.
        NameSpacer.Height = Math.Max(0, CellScroller.ActualHeight - CellScroller.ViewportHeight);
        NameScroller.ScrollToVerticalOffset(e.VerticalOffset);
    }

    // Mouse wheel over the names scrolls the cells too.
    private void OnNameScrollChanged(object sender, ScrollChangedEventArgs e) =>
        CellScroller.ScrollToVerticalOffset(e.VerticalOffset);

    // Arrow keys move between cells. Without this the scroll viewer scrolls instead
    // whenever the next cell is partly out of view.
    private void OnCellsPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is not UIElement focused) return;
        FocusNavigationDirection? direction = e.Key switch
        {
            Key.Left => FocusNavigationDirection.Left,
            Key.Right => FocusNavigationDirection.Right,
            Key.Up => FocusNavigationDirection.Up,
            Key.Down => FocusNavigationDirection.Down,
            _ => null,
        };
        if (direction is null) return;
        focused.MoveFocus(new TraversalRequest(direction.Value));
        e.Handled = true;
    }
}
