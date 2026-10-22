using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private sealed record DevelopmentCheck(string Name, bool Passed, string Detail, long Milliseconds);

    private async Task RunDevelopmentChecksAsync()
    {
        if (_developmentTesting || _closed) return;
        if (_timer.Active) { SetDevelopmentStatus("Stop the current timer before running UI checks."); return; }
        _developmentTesting = true;
        if (_developmentRunButton is not null) _developmentRunButton.IsEnabled = false;
        var original = ViewModel.Settings;
        var checks = new List<DevelopmentCheck>();
        var started = DateTimeOffset.UtcNow;
        SetDevelopmentStatus("Running UI checks…");
        async Task Check(string name, Func<Task> action)
        {
            var watch = Stopwatch.StartNew();
            try { await action(); checks.Add(new(name, true, "Passed", watch.ElapsedMilliseconds)); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                checks.Add(new(name, false, ex.Message, watch.ElapsedMilliseconds));
                _log.Write("error", "Development UI check failed: " + name, ex);
            }
            SetDevelopmentStatus($"Running: {checks.Count} checks, {checks.Count(c => !c.Passed)} failures.");
        }
        static void Require(bool condition, string failure)
        { if (!condition) throw new InvalidOperationException(failure); }
        async Task Settle()
        { await Task.Delay(750, _shutdown.Token); Root.UpdateLayout(); }
        try
        {
            await Check("Prepare isolated development session", async () =>
            {
                Require(await CommitAsync(s => s with { LockedProfile = Profile.Default, LiveTimer = true, LiveMedia = true,
                    CloseOnDeactivate = false, HideInFullscreen = false,
                    Workspaces = s.Workspaces.Select(w => w.Profile == Profile.Default ? w with { Widgets = w.Widgets.Append("Timer").Distinct().ToArray() } : w).ToArray()
                }), "Could not save temporary development preferences.");
            });
            foreach (var page in DevelopmentPages.All.Where(p => p != "Developer tools"))
                await Check("Page: " + page, async () =>
                {
                    OpenDevelopmentPage(page); await Task.Delay(100, _shutdown.Token); Root.UpdateLayout();
                    Require(_controlCenter && Expanded.Visibility == Visibility.Visible, "Control center did not open.");
                    if (DevelopmentPages.Settings.Contains(page))
                        Require(_settingsCategory == page && SettingsPage.Children.Count > 0, "Settings page is empty or incorrect.");
                });
            foreach (var theme in new[] { "Akeno", "Light", "Dark", "System" })
                await Check("Theme: " + theme, async () =>
                {
                    Require(await CommitAsync(s => s with { Theme = theme }), "Could not apply theme.");
                    await Settle(); Require(ViewModel.Settings.Theme == theme, "Theme preference did not apply.");
                    Require(Root.ActualWidth > 0 && Root.ActualHeight > 0, "Theme left an empty surface.");
                });
            await Check("Utilities timer and weather", async () =>
            {
                OpenControlCenter("Utilities"); await Settle();
                Require(UtilitiesPage.Visibility == Visibility.Visible && _utilityTimerRing!.ActualWidth > 0,
                    "Utilities timer ring did not appear.");
                Require(_utilityWeatherCity is not null && _utilityTimerPrimary is not null, "Utilities are incomplete.");
                TimerPrimary_Click(_utilityTimerPrimary!, new()); Require(_timer.Running, "Utilities did not start the timer.");
                TimerPrimary_Click(_utilityTimerPrimary!, new()); Require(_timer.Paused, "Utilities did not pause the timer.");
                TimerPrimary_Click(_utilityTimerPrimary!, new()); Require(_timer.Running, "Utilities did not resume the timer.");
                TimerCancel_Click(_utilityTimerPrimary!, new()); Require(!_timer.Active, "Utilities did not reset the timer.");
            });
            foreach (var edge in new[] { "Top edge", "Bottom edge", "Left edge", "Right edge" })
            {
                await Check(edge + ": notification", async () =>
                {
                    Require(await SetDevelopmentEdgeAsync(edge), "Could not change screen edge.");
                    ShowDevelopmentPreview("notification"); Collapse(); await Settle();
                    Require(_restingActivity == "notification" && _developmentNotification.ActualWidth > 0, "Notification is not visible.");
                    AssertDevelopmentActivityFits();
                });
                await Check(edge + ": media", async () =>
                {
                    ShowDevelopmentPreview("media"); Collapse(); await Settle();
                    Require(RestingArtworkHost.Visibility == Visibility.Visible && RestingVisualizerHost.Visibility == Visibility.Visible, "Artwork or visualizer is hidden.");
                    Require(RestingActivity.Orientation == (edge is "Top edge" or "Bottom edge" ? Microsoft.UI.Xaml.Controls.Orientation.Horizontal : Microsoft.UI.Xaml.Controls.Orientation.Vertical), "Activity orientation is incorrect.");
                    AssertDevelopmentActivityFits();
                });
                await Check(edge + ": countdown", async () =>
                {
                    ClearDevelopmentPreview(); _timer.Start(TimeSpan.FromSeconds(20)); _timerTick.Start();
                    UpdateTimerLabels(); RefreshLiveActivity(); Collapse(); await Settle();
                    Require(_restingActivity == "timer" && RestingCountdown.Text != "00:00", "Live countdown is not visible.");
                    AssertDevelopmentActivityFits(); _timer.Cancel(); _timerTick.Stop();
                });
            }
            foreach (var colorMode in new[] { "Custom", "System" })
                await Check("Visualizer color: " + colorMode, async () =>
                {
                    Require(await CommitAsync(s => s with { VisualizerColorMode = colorMode, VisualizerColor = "#39CCAB" }), "Could not save visualizer color.");
                    var expected = colorMode == "Custom" ? "#39CCAB" : $"#{VisualizerBrush.Color.R:X2}{VisualizerBrush.Color.G:X2}{VisualizerBrush.Color.B:X2}";
                    foreach (var visualizer in new[] { _restingVisualizer, PeekMusicVisualizer, OutputMusicVisualizer })
                        Require(Microsoft.UI.Xaml.Automation.AutomationProperties.GetHelpText(visualizer).Contains(expected), "Visualizer color did not update across media views.");
                });
            foreach (var glass in new[] { false, true })
                await Check("Notch material: glass=" + glass, async () =>
                {
                    Require(await CommitAsync(s => s with { Theme = "Dark", GlassEnabled = glass }), "Could not change material.");
                    ShowDevelopmentPreview("media"); Collapse(); await Settle();
                    Require(_glass.ContourMasked && _windowManager.AntialiasNotch, "Notch edge composition is inactive.");
                    AssertDevelopmentActivityFits();
                });
            await Check("Timer pause and resume", async () =>
            {
                ClearDevelopmentPreview();
                _timer.Start(TimeSpan.FromSeconds(3)); _timer.Pause(); var text = _timer.Text;
                await Task.Delay(250, _shutdown.Token);
                Require(_timer.Paused && _timer.Text == text, "Paused timer changed.");
                _timer.Resume(); Require(_timer.Running, "Timer did not resume."); _timer.Cancel();
            });
            await Check("Real timer completion and notch notification", async () =>
            {
                _timer.Start(TimeSpan.FromMilliseconds(500)); _timerTick.Start();
                UpdateTimerLabels(); RefreshLiveActivity(); Collapse();
                await Task.Delay(1800, _shutdown.Token); Root.UpdateLayout();
                Require(!_timer.Active && _restingActivity == "complete", "Timer did not complete.");
                Require(RestingComplete.Visibility == Visibility.Visible && RestingCountdown.Text == "00:00", "Completion check or 00:00 is missing.");
                AssertDevelopmentActivityFits();
            });
            foreach (var preview in new[] { PreviewKind.System, PreviewKind.Media, PreviewKind.Obs, PreviewKind.Apps, PreviewKind.Timer })
                await Check("Hover panel: " + preview, async () =>
                {
                    Collapse(); _dock.Hover(); ShowPreview(preview); await Settle();
                    Require(PreviewVisible && _preview == preview && PeekHost.ActualHeight > 0, "Hover panel did not open.");
                });
            await Check("Diagnostic state export", async () =>
            {
                await ExportDevelopmentStateAsync();
                Require(File.Exists(Path.Combine(DevelopmentDirectory, "diagnostics", "state.json")), "State report was not written.");
            });
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        finally
        {
            _timer.Cancel(); _timerTick.Stop(); ClearDevelopmentPreview();
            if (!_closed)
            {
                try
                {
                    var restored = await CommitAsync(_ => original);
                    checks.Add(new("Restore development preferences", restored, restored ? "Restored" : "Restore failed", 0));
                }
                catch (Exception ex) { checks.Add(new("Restore development preferences", false, ex.Message, 0)); _log.Write("error", "Could not restore development preferences", ex); }
            }
            _developmentTesting = false;
        }
        if (_closed) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DevelopmentReportPath)!);
            await File.WriteAllTextAsync(DevelopmentReportPath, JsonSerializer.Serialize(new
            {
                Channel = "Development", Version = CurrentVersion.ToString(3), Started = started, Finished = DateTimeOffset.UtcNow,
                Passed = checks.All(c => c.Passed), Checks = checks
            }, new JsonSerializerOptions { WriteIndented = true }));
            SetDevelopmentStatus($"{checks.Count(c => c.Passed)}/{checks.Count} checks passed. Report: {DevelopmentReportPath}");
            _log.Write("development", _developmentLastReport);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _log.Write("error", "Could not save UI check report", ex); SetDevelopmentStatus("Could not save report: " + ex.Message); }
        OpenDevelopmentPage("Developer tools");
    }

    private void AssertDevelopmentActivityFits()
    {
        var frame = _windowManager.CurrentFrame;
        var width = frame.Width / frame.Scale; var height = frame.Height / frame.Scale;
        var horizontal = ViewModel.Settings.Position.Orientation == "Horizontal";
        if (horizontal ? width <= height : height <= width) throw new InvalidOperationException("Notch proportions do not follow its screen edge.");
        var point = RestingActivity.TransformToVisual(Root).TransformPoint(new(0, 0));
        if (point.X < -1 || point.Y < -1 || point.X + RestingActivity.ActualWidth > width + 1 || point.Y + RestingActivity.ActualHeight > height + 1)
            throw new InvalidOperationException("Activity content is outside its window.");
        foreach (var x in new[] { point.X + 1, point.X + RestingActivity.ActualWidth - 1 })
            foreach (var y in new[] { point.Y + 1, point.Y + RestingActivity.ActualHeight - 1 })
                if (!_windowManager.ContainsNotchPoint(x * frame.Scale, y * frame.Scale))
                    throw new InvalidOperationException("Native notch geometry clips activity content.");
    }
}
