using System.Windows;
#if DEBUG
using WicklowArchery.App.Services;
using WicklowArchery.App.ViewModels;
#endif

namespace WicklowArchery.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
#if DEBUG
        // Sample mode: the test-form snapshot and four made-up people. No network, nothing saved.
        if (e.Args.Contains("--sample", StringComparer.OrdinalIgnoreCase))
        {
            var (form, data) = SampleData.Create();
            var week = new WeekViewModel();
            week.Load(form, data);
            window.DataContext = week;
        }
#endif
        MainWindow = window;
        window.Show();
    }
}
