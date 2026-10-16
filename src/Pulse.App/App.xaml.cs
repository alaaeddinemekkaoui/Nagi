using Microsoft.UI.Xaml;
using Pulse.Infrastructure;
using Pulse.Core;
using System.Diagnostics;

namespace Pulse.App;

public partial class App : Application
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly EventWaitHandle _setupActivate;
    private RegisteredWaitHandle? _setupWait;
    internal NagiLaunchOptions LaunchOptions { get; }
    private RegisteredWaitHandle? _wait;
    private MainWindow? _window;
    private WelcomeWindow? _welcome;
    private bool _released;
    private readonly DiagnosticLog _log;
    public App()
    {
        LaunchOptions = NagiLaunchOptions.Parse(Environment.GetCommandLineArgs().Skip(1));
        if (LaunchOptions.WaitForExit is { } parentId) WaitForPreviousProcess(parentId);
        _log = new(LaunchOptions.DataDirectory);
        _mutex = new Mutex(true, LaunchOptions.Scope("Local\\Pulse.Desktop.SingleInstance.v1"), out var first);
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, LaunchOptions.Scope("Local\\Pulse.Desktop.Activate.v1"));
        _setupActivate = new EventWaitHandle(false, EventResetMode.AutoReset, LaunchOptions.Scope("Local\\Pulse.Desktop.Setup.v1"));
        if (!first) { (LaunchOptions.ShowSetup ? _setupActivate : _activate).Set(); Environment.Exit(0); }
        InitializeComponent();
        UnhandledException += (_, args) => _log.Write("fatal", "Unhandled UI exception: " + args.Message + " | " + args.Exception.Message + " | " + args.Exception.StackTrace, args.Exception);
    }
    private static void WaitForPreviousProcess(int parentId)
    {
        try
        {
            using var parent = Process.GetProcessById(parentId);
            try
            {
                if (parent.HasExited) return;
                if (parentId == Environment.ProcessId || !string.Equals(parent.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The restart process does not belong to this Nagi installation.");
                if (!parent.WaitForExit(15000)) throw new InvalidOperationException("Nagi is still closing. Please open it again.");
            }
            catch (InvalidOperationException) when (parent.HasExited) { }
        }
        catch (ArgumentException) { } // Parent has already exited.
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = new SettingsService(LaunchOptions.DataDirectory);
        var configuration = await settings.LoadAsync();
        _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) =>
        {
            var welcome = _welcome; var window = _window;
            if (welcome is not null) welcome.DispatcherQueue.TryEnqueue(welcome.Activate);
            else if (window is not null) window.DispatcherQueue.TryEnqueue(window.Reveal);
        }, null, Timeout.Infinite, false);
        _setupWait = ThreadPool.RegisterWaitForSingleObject(_setupActivate, (_, _) =>
        {
            if (_welcome is { } welcome) welcome.DispatcherQueue.TryEnqueue(welcome.Activate);
            else if (_window is { } window) window.DispatcherQueue.TryEnqueue(window.RestartWelcome);
        }, null, Timeout.Infinite, false);
        if ((LaunchOptions.ShowSetup || !configuration.WelcomeCompleted) && configuration.SchemaVersion <= 1)
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
        _wait?.Unregister(null); _setupWait?.Unregister(null); _setupActivate.Dispose(); _activate.Dispose(); _mutex.ReleaseMutex(); _mutex.Dispose();
    }
}
