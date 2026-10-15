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
        RailGpuValue.Text = _gpuSnapshot.Usage is { } gpu ? $"{gpu:0}%" : "—";
        ToolTipService.SetToolTip(RailGpuButton, _gpuSnapshot.Name + " · " + _gpuSnapshot.Status);
        RailMicrophoneButton.IsEnabled = _microphoneSnapshot.Volume.HasValue;
        RailMicrophoneIcon.Glyph = _microphoneSnapshot.Muted ? "\uF781" : "\uE720";
        RailMicrophoneIcon.Foreground = ThemeBrush(_microphoneSnapshot.Muted ? "PulseAccentBrush" : "InkBrush");
        AutomationProperties.SetName(RailMicrophoneButton, _microphoneSnapshot.Muted ? "Unmute microphone" : "Mute microphone");
        ToolTipService.SetToolTip(RailMicrophoneButton, _microphoneSnapshot.Device + (_microphoneSnapshot.Muted ? " · Muted" : " · On"));
        var gpuLine = _gpuSnapshot.Name + "  " + (_gpuSnapshot.Usage is { } v ? $"{v:0}%" : "—");
        var sensorLine = _temperatureSnapshot.Readings.Length > 0 ? string.Join(" · ", _temperatureSnapshot.Readings.Take(4).Select(x => $"{x.Name} {x.Celsius:0}°C")) : "Temperature · connect a sensor provider";
        var fpsLine = _fpsSnapshot.Fps is { } f ? $"App FPS {f:0.0} · PID {_fpsSnapshot.ProcessId}" : _fpsSnapshot.Status;
        var micLine = "Microphone · " + (!_microphoneSnapshot.Volume.HasValue ? "Unavailable" : _microphoneSnapshot.Muted ? "Muted" : "On");
        var obsLine = !_obsSnapshot.Connected ? _obsSnapshot.Status : "OBS · " + (_obsSnapshot.Recording ? "Recording" : "Not recording") + (_obsSnapshot.Streaming ? " · Live" : "") + (_obsSnapshot.RenderFps is { } of ? $" · {of:0} render FPS" : "");
        IntegrationSummary.Text = gpuLine + "\n" + sensorLine + "\n" + fpsLine + "\n" + micLine + "\n" + obsLine;
        PeekIntegrations.Text = gpuLine + "\n" + sensorLine + "\n" + fpsLine;
        ObsRecordButton.IsEnabled = ViewModel.Settings.ObsEnabled && _obsSnapshot.Connected && !_obsActionInFlight;
        ObsRecordButton.Content = _obsSnapshot.Recording ? "Stop recording" : "OBS record";
    }
    private async void Microphone_Click(object sender, RoutedEventArgs args)
    {
        var result = await Task.Run(() => { var current = _microphone.Sample(); return current.Volume.HasValue ? _microphone.SetMute(!current.Muted) : OperationResult.Fail("No default communications microphone is available."); });
        Report(result); _microphoneSnapshot = await Task.Run(_microphone.Sample); if (!_closed) RenderIntegrations();
    }
    private async void ObsRecord_Click(object sender, RoutedEventArgs args)
    {
        if (!ViewModel.Settings.ObsEnabled || _obsActionInFlight) return;
        _obsActionInFlight = true;
        ObsRecordButton.IsEnabled = false;
        try { Report(await _obs.ToggleRecordingAsync(ViewModel.Settings.ObsPort, Volatile.Read(ref _obsPassword), _shutdown.Token)); }
        finally { _obsActionInFlight = false; if (!_closed) RenderIntegrations(); }
    }
    private void BuildIntegrationSettings()
    {
        var integrations = Stack(); integrations.Children.Add(Heading("Hardware & studio"));
        integrations.Children.Add(Note(_gpuSnapshot.Name + "\n" + _gpuSnapshot.Status));
        integrations.Children.Add(Note("Temperatures: " + _temperatureSnapshot.Status + ". Run Libre Hardware Monitor or Open Hardware Monitor with WMI enabled to expose supported CPU/GPU sensors. Nagi does not install a kernel driver."));
        integrations.Children.Add(Toggle("Capture foreground app FPS", "Uses bundled PresentMon 2.6.0. Some Windows configurations require tracing permission. No keyboard/mouse input is tracked.", ViewModel.Settings.FpsEnabled, (s, enabled) => s with { FpsEnabled = enabled }));
        integrations.Children.Add(Note(_fpsSnapshot.Status + "\nFPS means application presents per second, not monitor refresh rate. The provider uses a separate, short-lived trace for the foreground process. If Windows denies access, run Start-NagiFpsCapture.ps1 for your game from an Administrator PowerShell; Nagi itself can stay unprivileged."));
        integrations.Children.Add(Button("Toggle microphone mute", Microphone_Click));
        integrations.Children.Add(Note("Controls the default communications microphone. Nagi does not record audio. Device: " + _microphoneSnapshot.Device));
        SettingsPage.Children.Add(Card(integrations));
        var obs = Stack(); obs.Children.Add(Heading("OBS Studio"));
        obs.Children.Add(Note("Enable the OBS WebSocket server in OBS → Tools → WebSocket Server Settings. Nagi connects only to this computer. Recording changes require a button click."));
        obs.Children.Add(Toggle("Connect to OBS", "Read recording, stream and rendering status through OBS WebSocket v5.", ViewModel.Settings.ObsEnabled, (s, enabled) => s with { ObsEnabled = enabled }));
        var port = new NumberBox { Header = "OBS WebSocket port", Minimum = 1024, Maximum = 65535, Value = ViewModel.Settings.ObsPort, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        obs.Children.Add(port);
        var password = new PasswordBox { Header = "OBS password", PlaceholderText = _obsSecret!.Exists ? "Saved securely · enter to replace" : "Server password", MaxLength = 512 };
        obs.Children.Add(password);
        obs.Children.Add(Button("Save OBS connection", async (_, _) =>
        {
            if (!double.IsFinite(port.Value) || port.Value is < 1024 or > 65535) { ViewModel.Publish("Choose a port between 1024 and 65535.", true); return; }
            try { if (password.Password.Length > 0) { _obsSecret.Save(password.Password); Volatile.Write(ref _obsPassword, password.Password); password.Password = ""; } }
            catch (Exception ex) { _log.Write("obs", "Could not protect the OBS password", ex); ViewModel.Publish("The OBS password could not be saved.", true); return; }
            await CommitAsync(s => s with { ObsPort = (int)port.Value }); BuildSettingsPage();
        }));
        obs.Children.Add(Button("Clear saved OBS password", (_, _) => { _obsSecret.Clear(); Volatile.Write(ref _obsPassword, ""); BuildSettingsPage(); }));
        obs.Children.Add(Note(_obsSnapshot.Status + " · Passwords are protected for your Windows user account, separately from settings.json."));
        SettingsPage.Children.Add(Card(obs));
    }
}
