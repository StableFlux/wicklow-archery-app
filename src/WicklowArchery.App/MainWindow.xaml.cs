using System.Windows;
using System.Windows.Input;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;

namespace WicklowArchery.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Put the window back where it was last time, if that spot is still on a screen.</summary>
    public void RestorePlacement(WindowPlacement? placement)
    {
        if (placement is null || placement.Width < MinWidth || placement.Height < MinHeight)
            return;

        // Needs a visible strip of at least 100 x 50 on the combined screen area.
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var saved = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        var overlap = Rect.Intersect(screen, saved);
        if (overlap.IsEmpty || overlap.Width < 100 || overlap.Height < 50)
            return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        if (placement.Maximized)
            WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is not MainViewModel vm)
            return;

        var maximized = WindowState == WindowState.Maximized;
        var bounds = maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        if (WindowState != WindowState.Minimized && !bounds.IsEmpty)
        {
            vm.Data.Settings.Window = new WindowPlacement
            {
                Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height, Maximized = maximized,
            };
        }
        vm.SaveData();
    }

    private void StatusPill_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Status: FormStatus.Changed })
            ProblemsPopup.IsOpen = true;
    }
}
