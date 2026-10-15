using Microsoft.UI.Xaml;
using Pulse.Infrastructure;

namespace Pulse.App;

public partial class App : Application
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _wait;
    private MainWindow? _window;
    private readonly DiagnosticLog _log = new();
    public App()
    {
        _mutex = new Mutex(true, "Local\\Pulse.Desktop.SingleInstance.v1", out var first);
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Pulse.Desktop.Activate.v1");
        if (!first) { _activate.Set(); Environment.Exit(0); }
        InitializeComponent();
        UnhandledException += (_, args) => _log.Write("fatal", "Unhandled UI exception: " + args.Message + " | " + args.Exception.Message + " | " + args.Exception.StackTrace, args.Exception);
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = new SettingsService();
        var configuration = await settings.LoadAsync();
        _window = new MainWindow(settings, configuration, _log);
        _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => _window.DispatcherQueue.TryEnqueue(() => _window.Reveal()), null, Timeout.Infinite, false);
        _window.Closed += (_, _) => { _wait?.Unregister(null); _activate.Dispose(); _mutex.ReleaseMutex(); _mutex.Dispose(); };
        _window.ShowInitial();
        _log.Write("lifecycle", "Nagi started");
    }
}
