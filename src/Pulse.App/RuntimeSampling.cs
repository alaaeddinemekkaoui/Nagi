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
        var hardware = visible && _controlCenter && _page == "Settings" && _settingsCategory == "Hardware & studio";
        var media = shown && (overview && AudioCard.Visibility == Visibility.Visible
            || PreviewVisible && _preview == PreviewKind.Media && PeekMusicVisualizer.Visibility == Visibility.Visible);
        var performance = widgets && workspace.Shows("Performance");
        var peak = visible && (media || RestingMediaVisible || !_controlCenter && widgets && CompactMusicVisualizer.Visibility == Visibility.Visible);
        var next = new RuntimeDemand(
            performance || hardware,
            widgets && workspace.Shows("Microphone") || hardware,
            performance && (overview || PreviewVisible && _preview == PreviewKind.System) || hardware,
            ViewModel.Settings.FpsEnabled && (shown && workspace.Shows("Performance") || hardware),
            peak ? 2 : shown ? 1 : 0, ViewModel.Foreground.ProcessId);
        var previous = Volatile.Read(ref _runtimeDemand);
        if (next == previous) return;
        Volatile.Write(ref _runtimeDemand, next);
        if (next.Audio != previous.Audio) Wake(_audioWake);
        if (next.Gpu != previous.Gpu || next.Microphone != previous.Microphone || next.Sensors != previous.Sensors || next.Fps != previous.Fps)
            Wake(_integrationWake);
        _log.Write("sampling", $"audio={next.AudioIntervalMs}ms; peak={next.AudioPeak}; gpu={next.Gpu}; mic={next.Microphone}; sensors={next.Sensors}; fps={next.Fps}");
    }
}
