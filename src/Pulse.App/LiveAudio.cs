using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private Task? _audioPolling;
    private readonly global::Windows.UI.ViewManagement.UISettings _audioMotion = new();
    private AudioSnapshot _latestAudio = new(null, false, "Checking audio…");
    private int _audioFrameQueued;
    private long _audioSamples, _audioPeakSamples;
    private long _audioFrameCount;
    private bool _volumeWriteInFlight;
    private readonly AudioEnvelope _audioEnvelope = new();
    private long _audioRenderedAt = Stopwatch.GetTimestamp();

    private async Task PollAudioAsync()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            var demand = Volatile.Read(ref _runtimeDemand);
            _audioSamples++; if (demand.AudioPeak) _audioPeakSamples++;
            Volatile.Write(ref _latestAudio, _audio.Sample(demand.AudioPeak));
            if (demand.Audio > 0 && Interlocked.Exchange(ref _audioFrameQueued, 1) == 0)
            {
                if (!DispatcherQueue.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref _audioFrameQueued, 0);
                    if (_closed) return;
                    var current = Volatile.Read(ref _latestAudio);
                    ViewModel.ApplyAudio(current); RenderAudio(current);
                })) Interlocked.Exchange(ref _audioFrameQueued, 0);
            }
            try { await _audioWake.WaitAsync(demand.AudioIntervalMs, token); } catch (OperationCanceledException) { break; }
        }
        _log.Write("sampling", $"Audio stopped: samples={_audioSamples}; peak samples={_audioPeakSamples}; UI frames={_audioFrameCount}");
    }

    private void RenderAudio(AudioSnapshot audio)
    {
        if (_closed) return;
        _audioFrameCount++;
        _lastAudio = audio;
        RenderRestingAudio(audio);
        if (_userHidden || _dock.Stage == DockStage.Resting) return;
        var previous = _updating; _updating = true;
        foreach (var slider in new[] { VolumeSlider, PeekVolumeSlider })
        {
            slider.IsEnabled = audio.Volume.HasValue;
            if (!_volumeTimer.IsEnabled && !_volumeWriteInFlight && !(_windowManager.IsPrimaryButtonPressed && PointerOver(slider))
                && Math.Abs(slider.Value - (audio.Volume ?? 0)) > .1) slider.Value = audio.Volume ?? 0;
        }
        _updating = previous;
        MuteButton.IsEnabled = PeekMuteButton.IsEnabled = audio.Volume.HasValue;
        VolumeValue.Text = PeekVolumeValue.Text = audio.Volume is { } volume ? audio.Muted ? L.T("Muted") : L.F($"{volume:0}%") : L.T("Unavailable");
        CompactAudioValue.Text = audio.Volume is { } v ? audio.Muted ? L.T("Muted") : L.F($"{v:0}% vol") : L.T("No audio");
        MuteIcon.Glyph = PeekMuteIcon.Glyph = audio.Muted ? "\uE74F" : "\uE767";
        var now = Stopwatch.GetTimestamp();
        var level = _audioEnvelope.Update(audio.Muted ? 0 : audio.Peak, Stopwatch.GetElapsedTime(_audioRenderedAt, now).TotalSeconds);
        _audioRenderedAt = now;
        var accent = VisualizerBrush;
        var overview = Expanded.Visibility == Microsoft.UI.Xaml.Visibility.Visible && _page == "Overview" && AudioCard.Visibility == Microsoft.UI.Xaml.Visibility.Visible;
        var mediaPeek = PeekHost.Visibility == Microsoft.UI.Xaml.Visibility.Visible && _preview == PreviewKind.Media;
        if (overview) OutputAudioMeter.Update(level, audio.Peak.HasValue, accent);
        var animate = !ViewModel.Settings.ReducedMotion && _audioMotion.AnimationsEnabled;
        if (!_userHidden)
        {
            if (!_controlCenter && CompactMusicVisualizer.Visibility == Microsoft.UI.Xaml.Visibility.Visible) CompactMusicVisualizer.Update(level, audio.Peak.HasValue, accent, animate);
            if (mediaPeek && PeekMusicVisualizer.Visibility == Microsoft.UI.Xaml.Visibility.Visible) PeekMusicVisualizer.Update(level, audio.Peak.HasValue, accent, animate);
            if (overview) OutputMusicVisualizer.Update(level, audio.Peak.HasValue, accent, animate);
        }
    }
}
