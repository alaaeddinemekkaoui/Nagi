using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly GpuTelemetryService _gpu = new();
    private readonly TemperatureService _temperatures = new();
    private readonly AudioControlService _microphone = new(capture: true);
    private readonly ObsService _obs = new();
    private readonly FpsService _fps = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Tools", "PresentMon.exe"));
    private ProtectedSecretStore? _obsSecret;
    private string _obsPassword = "";
    private Task? _integrationPolling;
    private bool _obsActionInFlight;
    private GpuSnapshot _gpuSnapshot = new(null, "GPU", "Checking Windows counters…");
    private TemperatureSnapshot _temperatureSnapshot = new([], "Checking sensor providers…");
    private FpsSnapshot _fpsSnapshot = new(null, 0, "FPS capture off");
    private ObsSnapshot _obsSnapshot = new(false, false, false, null, "OBS connection off");
    private AudioSnapshot _microphoneSnapshot = new(null, false, "Checking microphone…");
    private void InitializeIntegrations()
    {
        _obsSecret = new(Path.Combine(Path.GetDirectoryName(_settings.FilePath)!, "obs.credential"));
        _obsPassword = _obsSecret.Read();
    }
    private async Task PollIntegrationsAsync()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var settings = Volatile.Read(ref _pollSettings);
                var gpu = _gpu.Sample(); var microphone = _microphone.Sample();
                DispatcherQueue.TryEnqueue(() => { if (!_closed) { _gpuSnapshot = gpu; _microphoneSnapshot = microphone; RenderIntegrations(); } });
                var temperature = Task.Run(_temperatures.Sample, token);
                var fps = _fps.SampleAsync(settings.FpsEnabled, ViewModel.Foreground.ProcessId, token);
                var obs = _obs.SampleAsync(settings.ObsEnabled, settings.ObsPort, Volatile.Read(ref _obsPassword), token);
                await Task.WhenAll(temperature, fps, obs);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_closed) return;
                    _temperatureSnapshot = temperature.Result; _fpsSnapshot = fps.Result; _obsSnapshot = obs.Result;
                    RenderIntegrations();
                });
                await Task.Delay(1500, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.Write("integrations", "Integration refresh failed", ex);
                try { await Task.Delay(3000, token); } catch (OperationCanceledException) { break; }
            }
        }
    }
    private void RenderIntegrations()
    {
        GpuMetric.Update("GPU", _gpuSnapshot.Usage is { } usage ? $"{usage:0}%" : "—", _gpuSnapshot.Name, _gpuSnapshot.Usage);
        RailGpuValue.Text = _gpuSnapshot.Usage is { } gpu ? L.F($"{gpu:0}%") : "—";
        ToolTipService.SetToolTip(RailGpuButton, _gpuSnapshot.Name + " · " + _gpuSnapshot.Status);
        RailMicrophoneButton.IsEnabled = _microphoneSnapshot.Volume.HasValue;
        RailMicrophoneIcon.Glyph = _microphoneSnapshot.Muted ? "\uF781" : "\uE720";
        RailMicrophoneIcon.Foreground = ThemeBrush(_microphoneSnapshot.Muted ? "PulseAccentBrush" : "InkBrush");
        AutomationProperties.SetName(RailMicrophoneButton, _microphoneSnapshot.Muted ? L.T("Unmute microphone") : L.T("Mute microphone"));
        ToolTipService.SetToolTip(RailMicrophoneButton, _microphoneSnapshot.Device + (_microphoneSnapshot.Muted ? L.T(" · Muted") : L.T(" · On")));
        var gpuLine = _gpuSnapshot.Name + "  " + (_gpuSnapshot.Usage is { } v ? $"{v:0}%" : "—");
        var sensorLine = _temperatureSnapshot.Readings.Length > 0 ? string.Join(" · ", _temperatureSnapshot.Readings.Take(4).Select(x => $"{x.Name} {x.Celsius:0}°C")) : "Temperature · connect a sensor provider";
        var fpsLine = _fpsSnapshot.Fps is { } f ? $"App FPS {f:0.0} · PID {_fpsSnapshot.ProcessId}" : _fpsSnapshot.Status;
        var micLine = "Microphone · " + (!_microphoneSnapshot.Volume.HasValue ? "Unavailable" : _microphoneSnapshot.Muted ? "Muted" : "On");
        MicrophoneDeviceText.Text = _microphoneSnapshot.Device;
        MicrophoneStatusText.Text = !_microphoneSnapshot.Volume.HasValue ? L.T("No microphone available") : _microphoneSnapshot.Muted ? L.T("Muted") : L.T("On");
        MicrophoneCardIcon.Glyph = RailMicrophoneIcon.Glyph;
        MicrophoneToggleButton.IsEnabled = PeekMicrophoneButton.IsEnabled = RailMicrophoneButton.IsEnabled;
        MicrophoneToggleButton.Content = _microphoneSnapshot.Muted ? L.T("Unmute") : L.T("Mute");
        PeekMicrophoneButton.Content = _microphoneSnapshot.Muted ? L.T("Unmute mic") : L.T("Mute mic");
        PeekMicrophoneText.Text = micLine;
        var obsLine = !_obsSnapshot.Connected ? _obsSnapshot.Status : "OBS · " + (_obsSnapshot.Recording ? "Recording" : "Not recording") + (_obsSnapshot.Streaming ? " · Live" : "") + (_obsSnapshot.RenderFps is { } of ? $" · {of:0} render FPS" : "");
        IntegrationSummary.Text = gpuLine + "\n" + sensorLine + "\n" + fpsLine + (ViewModel.Settings.Workspace(ViewModel.Profile).Shows("Microphone") ? "\n" + micLine : "") + "\n" + obsLine;
        PeekIntegrations.Text = gpuLine + "\n" + sensorLine + "\n" + fpsLine;
        PeekObsStatus.Text = obsLine + (_obsSnapshot.CurrentScene is { } scene ? L.T("\nScene: ") + scene : "");
        ObsRecordButton.IsEnabled = PeekObsRecordButton.IsEnabled = ObsStreamButton.IsEnabled = PeekObsStreamButton.IsEnabled = ViewModel.Settings.ObsEnabled && _obsSnapshot.Connected && !_obsActionInFlight;
        ObsRecordButton.Content = PeekObsRecordButton.Content = _obsSnapshot.Recording ? L.T("Stop recording") : L.T("Start recording");
        ObsStreamButton.Content = PeekObsStreamButton.Content = _obsSnapshot.Streaming ? L.T("Stop streaming") : L.T("Start streaming");
        RenderObsScenes();
    }
    private async void Microphone_Click(object sender, RoutedEventArgs args)
    {
        var result = await Task.Run(() => { var current = _microphone.Sample(); return current.Volume.HasValue ? _microphone.SetMute(!current.Muted) : OperationResult.Fail("No default communications microphone is available."); });
        Report(result); _microphoneSnapshot = await Task.Run(_microphone.Sample); if (!_closed) RenderIntegrations();
    }
    private async void ObsOpen_Click(object sender, RoutedEventArgs args)
    {
        try { Report(await OpenProfileAppAsync(new("OBS Studio", "obs", "Suggested"))); }
        catch (OperationCanceledException) { }
    }
    private async void ObsRecord_Click(object sender, RoutedEventArgs args) => await RunObsActionAsync(() => _obs.ToggleRecordingAsync(ViewModel.Settings.ObsPort, Volatile.Read(ref _obsPassword), _shutdown.Token));
    private async void ObsStream_Click(object sender, RoutedEventArgs args) => await RunObsActionAsync(() => _obs.ToggleStreamingAsync(ViewModel.Settings.ObsPort, Volatile.Read(ref _obsPassword), _shutdown.Token));
    private async Task RunObsActionAsync(Func<Task<OperationResult>> action)
    {
        if (!ViewModel.Settings.ObsEnabled || !_obsSnapshot.Connected || _obsActionInFlight) return;
        _obsActionInFlight = true; RenderIntegrations();
        try
        {
            Report(await action());
            var snapshot = await _obs.SampleAsync(ViewModel.Settings.ObsEnabled, ViewModel.Settings.ObsPort, Volatile.Read(ref _obsPassword), _shutdown.Token);
            if (!_closed) _obsSnapshot = snapshot;
        }
        catch (OperationCanceledException) { }
        finally { _obsActionInFlight = false; if (!_closed) RenderIntegrations(); }
    }
    private string? _obsScenesKey;
    private void RenderObsScenes()
    {
        var configured = ViewModel.Settings.ObsScenes;
        var scenes = Enumerable.Range(0, 3).Select(i => string.IsNullOrEmpty(configured[i]) ? _obsSnapshot.Scenes.ElementAtOrDefault(i) : configured[i]).ToArray();
        var enabled = ViewModel.Settings.ObsEnabled && _obsSnapshot.Connected && !_obsActionInFlight;
        var key = System.Text.Json.JsonSerializer.Serialize(new { scenes, enabled, current = _obsSnapshot.CurrentScene, available = _obsSnapshot.Scenes });
        if (_obsScenesKey == key) return; _obsScenesKey = key;
        foreach (var panel in new[] { ObsScenesPanel, PeekObsScenesPanel })
        {
            panel.Children.Clear();
            for (var i = 0; i < 3; i++)
            {
                var scene = scenes[i];
                var button = Button(scene ?? L.T("Scene ") + (i + 1), async (_, _) => await RunObsActionAsync(() => _obs.SwitchSceneAsync(ViewModel.Settings.ObsPort, Volatile.Read(ref _obsPassword), scene ?? "", _shutdown.Token)));
                button.IsEnabled = enabled && scene is not null && _obsSnapshot.Scenes.Contains(scene);
                button.FontSize = 12; button.Padding = new(8); button.HorizontalAlignment = HorizontalAlignment.Stretch;
                Grid.SetColumn(button, i);
                button.Content = new TextBlock { Text = (_obsSnapshot.CurrentScene == scene && scene is not null ? "* " : "") + (scene ?? L.T("Scene ") + (i + 1)), TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
                AutomationProperties.SetName(button, L.T("Switch OBS scene ") + (i + 1) + ": " + (scene ?? L.T("Not configured")));
                ToolTipService.SetToolTip(button, scene ?? L.T("Connect OBS and choose a scene in Hardware & studio settings."));
                panel.Children.Add(button);
            }
        }
    }
    private void BuildIntegrationSettings()
    {
        var integrations = Stack(); integrations.Children.Add(Heading(L.T("Hardware & studio")));
        integrations.Children.Add(Note(_gpuSnapshot.Name + "\n" + _gpuSnapshot.Status));
        integrations.Children.Add(Note(L.T("Temperatures: ") + _temperatureSnapshot.Status + L.T(". Run Libre Hardware Monitor or Open Hardware Monitor with WMI enabled to expose supported CPU/GPU sensors. Nagi does not install a kernel driver.")));
        integrations.Children.Add(Toggle(L.T("Capture foreground app FPS"), L.T("Uses bundled PresentMon 2.6.0. Some Windows configurations require tracing permission. No keyboard/mouse input is tracked."), ViewModel.Settings.FpsEnabled, (s, enabled) => s with { FpsEnabled = enabled }));
        integrations.Children.Add(Note(_fpsSnapshot.Status + L.T("\nFPS means application presents per second, not monitor refresh rate. The provider uses a separate, short-lived trace for the foreground process. If Windows denies access, run Start-NagiFpsCapture.ps1 for your game from an Administrator PowerShell; Nagi itself can stay unprivileged.")));
        integrations.Children.Add(Button(L.T("Toggle microphone mute"), Microphone_Click));
        integrations.Children.Add(Note(L.T("Controls the default communications microphone. Nagi does not record audio. Device: ") + _microphoneSnapshot.Device));
        SettingsPage.Children.Add(Card(integrations));
        var obs = Stack(); obs.Children.Add(Heading(L.T("OBS Studio")));
        obs.Children.Add(Note(L.T("Enable the OBS WebSocket server in OBS → Tools → WebSocket Server Settings. Nagi connects only to this computer. Recording, streaming and scene changes require a button click.")));
        obs.Children.Add(Toggle(L.T("Connect to OBS"), L.T("Read recording, stream and rendering status through OBS WebSocket v5."), ViewModel.Settings.ObsEnabled, (s, enabled) => s with { ObsEnabled = enabled }));
        var port = new NumberBox { Header = L.T("OBS WebSocket port"), Minimum = 1024, Maximum = 65535, Value = ViewModel.Settings.ObsPort, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        obs.Children.Add(port);
        var password = new PasswordBox { Header = L.T("OBS password"), PlaceholderText = _obsSecret!.Exists ? L.T("Saved securely · enter to replace") : L.T("Server password"), MaxLength = 512 };
        obs.Children.Add(password);
        obs.Children.Add(Button(L.T("Save OBS connection"), async (_, _) =>
        {
            if (!double.IsFinite(port.Value) || port.Value is < 1024 or > 65535) { ViewModel.Publish(L.T("Choose a port between 1024 and 65535."), true); return; }
            try { if (password.Password.Length > 0) { _obsSecret.Save(password.Password); Volatile.Write(ref _obsPassword, password.Password); password.Password = ""; } }
            catch (Exception ex) { _log.Write("obs", "Could not protect the OBS password", ex); ViewModel.Publish(L.T("The OBS password could not be saved."), true); return; }
            await CommitAsync(s => s with { ObsPort = (int)port.Value }); BuildSettingsPage();
        }));
        obs.Children.Add(Button(L.T("Clear saved OBS password"), (_, _) => { _obsSecret.Clear(); Volatile.Write(ref _obsPassword, ""); BuildSettingsPage(); }));
        obs.Children.Add(Heading(L.T("Three scene shortcuts")));
        obs.Children.Add(Note(L.T("Connect OBS, then choose the scenes for your three buttons. Automatic uses the first three scenes in OBS.")));
        for (var i = 0; i < 3; i++)
        {
            var slot = i;
            var names = new[] { "Automatic" }.Concat(_obsSnapshot.Scenes).Concat(ViewModel.Settings.ObsScenes.Where(n => n.Length > 0)).Distinct().ToArray();
            var choice = Select(names, ViewModel.Settings.ObsScenes[i].Length > 0 ? ViewModel.Settings.ObsScenes[i] : "Automatic", "OBS scene shortcut " + (i + 1));
            choice.SelectionChanged += async (_, _) =>
            {
                if (choice.SelectedItem is not string scene) return;
                await CommitAsync(s => s with { ObsScenes = s.ObsScenes.Select((name, index) => index == slot ? scene == "Automatic" ? "" : scene : name).ToArray() });
                RenderIntegrations();
            };
            obs.Children.Add(Labeled(L.T("Scene button ") + (i + 1), choice));
        }
        obs.Children.Add(Button(L.T("Refresh OBS scenes"), async (_, _) =>
        {
            var snapshot = await _obs.SampleAsync(ViewModel.Settings.ObsEnabled, ViewModel.Settings.ObsPort, Volatile.Read(ref _obsPassword), _shutdown.Token);
            if (!_closed) { _obsSnapshot = snapshot; RenderIntegrations(); BuildSettingsPage(); }
        }));
        obs.Children.Add(Note(_obsSnapshot.Status + L.T(" · Passwords are protected for your Windows user account, separately from settings.json.")));
        SettingsPage.Children.Add(Card(obs));
    }
}
