using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WicklowArchery.App.Services;
using WicklowArchery.App.ViewModels;
using WicklowArchery.Core.Data;
using WicklowArchery.Core.Net;
using WicklowArchery.Core.Registration;

namespace WicklowArchery.App;

public partial class App : Application
{
    // One copy at a time: two windows would each save their own history over the other's.
    // Debug builds (including --sample) use their own name so they never block an installed copy.
#if DEBUG
    private const string InstanceMutexName = @"Local\WicklowArchers.SingleInstance.Dev";
#else
    private const string InstanceMutexName = @"Local\WicklowArchers.SingleInstance";
#endif

    private Mutex? _instanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Already running: bring that window forward and leave before any window or store exists.
            mutex.Dispose();
            ActivateOtherInstance();
            Shutdown();
            return;
        }
        _instanceMutex = mutex;

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
        try { await vm.InitializeAsync(); } catch (Exception) { /* the view model reports its own failures; never crash at launch */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instanceMutex is not null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned; nothing to release */ }
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
        base.OnExit(e);
    }

    /// <summary>Restores and focuses the main window of the copy that is already running (same program file).</summary>
    private static void ActivateOtherInstance()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            var path = current.MainModule?.FileName;
            foreach (var other in Process.GetProcessesByName(current.ProcessName))
            {
                using (other)
                {
                    if (other.Id == current.Id) continue;
                    string? otherPath;
                    try { otherPath = other.MainModule?.FileName; }
                    catch (Exception) { otherPath = null; }
                    if (path is not null && otherPath is not null && !string.Equals(path, otherPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var handle = other.MainWindowHandle;
                    if (handle == IntPtr.Zero) continue;
                    if (IsIconic(handle)) ShowWindow(handle, SwRestore);
                    SetForegroundWindow(handle);
                    return;
                }
            }
        }
        catch (Exception) { /* best effort: this copy closes either way */ }
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);
}
