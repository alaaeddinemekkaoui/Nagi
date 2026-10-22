using Microsoft.UI.Xaml;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private RuntimeDemand _runtimeDemand = new();
    private readonly SemaphoreSlim _audioWake = new(0, 1);
    private readonly SemaphoreSlim _integrationWake = new(0, 1);
    private static void Wake(SemaphoreSlim signal)
    { try { signal.Release(); } catch (SemaphoreFullException) { } }

    private void UpdateRuntimeDemand()
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        var visible = !_userHidden && _windowManager.IsVisible;
        var shown = visible && _dock.Stage != DockStage.Resting;
        var overview = shown && _expanded && _page == "Overview";
        var widgets = shown && (!_controlCenter || overview);
        UpdateModeDemand(shown && (widgets || overview || PreviewVisible && _preview == PreviewKind.Mode));
        var hardware = visible && _controlCenter && _page == "Settings" && _settingsCategory == "Hardware & audio";
        var integrations = visible && _controlCenter && _page == "Settings" && _settingsCategory is "Integrations" or "Hardware & studio";
        var media = shown && (overview && AudioCard.Visibility == Visibility.Visible
            || PreviewVisible && _preview == PreviewKind.Media && PeekMusicVisualizer.Visibility == Visibility.Visible);
        var performance = widgets && workspace.Shows("Performance");
        var peak = visible && (media || RestingMediaVisible || !_controlCenter && widgets && CompactMusicVisualizer.Visibility == Visibility.Visible);
        var creatorMeter = visible && ViewModel.Settings.Workspace(ViewModel.Profile).ModeEnabled && ModePresets.For(workspace) == "Creator" && (overview || PreviewVisible && _preview == PreviewKind.Mode);
        var next = new RuntimeDemand(
            performance || hardware,
            widgets && workspace.Shows("Microphone") || hardware,
            performance && (overview || PreviewVisible && _preview is PreviewKind.System or PreviewKind.Mode) || hardware,
            ViewModel.Settings.FpsEnabled && (performance && workspace.WantsFps() || integrations),
            peak ? 2 : shown ? 1 : 0, ViewModel.Foreground.ProcessId) { MicrophonePeak = creatorMeter };
        var previous = Volatile.Read(ref _runtimeDemand);
        if (next == previous) return;
        Volatile.Write(ref _runtimeDemand, next);
        if (next.Audio != previous.Audio || next.MicrophonePeak != previous.MicrophonePeak) Wake(_audioWake);
        if (next.Gpu != previous.Gpu || next.Microphone != previous.Microphone || next.Sensors != previous.Sensors || next.Fps != previous.Fps || next.ProcessId != previous.ProcessId && next.Fps)
            Wake(_integrationWake);
        _log.Write("sampling", $"audio={next.AudioIntervalMs}ms; peak={next.AudioPeak}; gpu={next.Gpu}; mic={next.Microphone}; micPeak={next.MicrophonePeak}; sensors={next.Sensors}; fps={next.Fps}");
    }
}
