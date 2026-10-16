using Microsoft.UI.Xaml;
using Pulse.Infrastructure;

namespace Pulse.App;

public partial class App : Application
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _wait;
    private MainWindow? _window;
    private WelcomeWindow? _welcome;
    private bool _released;
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
        _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) =>
        {
            var welcome = _welcome; var window = _window;
            if (welcome is not null) welcome.DispatcherQueue.TryEnqueue(welcome.Activate);
            else if (window is not null) window.DispatcherQueue.TryEnqueue(window.Reveal);
        }, null, Timeout.Infinite, false);
        if (!configuration.WelcomeCompleted && configuration.SchemaVersion <= 1)
        {
            var welcome = _welcome = new WelcomeWindow(settings, configuration);
            welcome.Finished += completed => { StartDock(settings, completed); _welcome = null; welcome.Close(); };
            welcome.Closed += (_, _) => { if (!welcome.Completed) { _welcome = null; settings.Dispose(); ReleaseInstance(); Exit(); } };
            welcome.Activate(); return;
        }
        StartDock(settings, configuration);
    }
    private void StartDock(SettingsService settings, Pulse.Core.PulseSettings configuration)
    {
        _window = new MainWindow(settings, configuration, _log);
        _window.Closed += (_, _) => ReleaseInstance();
        _window.ShowInitial();
        _log.Write("lifecycle", "Nagi started");
    }
    private void ReleaseInstance()
    {
        if (_released) return; _released = true;
        _wait?.Unregister(null); _activate.Dispose(); _mutex.ReleaseMutex(); _mutex.Dispose();
    }
}
