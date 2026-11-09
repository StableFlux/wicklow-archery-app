using System.Windows;
using WicklowArchery.App.Services;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;

namespace WicklowArchery.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IFormClient client;
        AppDataStore store;
#if DEBUG
        // Sample mode: the test-form snapshot and four made-up people, in a temp folder. No network.
        if (e.Args.Contains("--sample", StringComparer.OrdinalIgnoreCase))
        {
            client = new SampleFormClient();
            store = SampleData.CreateStore();
        }
        else
        {
            // Debug builds can never POST to the club's real form.
            client = new ClubFormGuardClient(HttpFormClient.Create());
            store = new AppDataStore(AppDataStore.DefaultDirectory, new DpapiProtector());
        }
#else
        client = HttpFormClient.Create();
        store = new AppDataStore(AppDataStore.DefaultDirectory, new DpapiProtector());
#endif

        var vm = new MainViewModel(new RegistrationService(client, TimeProvider.System), store, new ShellUrlOpener());
        var window = new MainWindow { DataContext = vm };
        window.RestorePlacement(vm.Data.Settings.Window);
        MainWindow = window;
        window.Show();

        // Window first, so "Checking this week's form…" is visible while the form loads.
        await vm.InitializeAsync();
    }
}
