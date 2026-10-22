using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Windows.System;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private string _developmentPreview = "";
    private DateTimeOffset _developmentPreviewUntil;
    private readonly TextBlock _developmentNotification = new()
    {
        Text = "Test notification", FontSize = 11, TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center, MaxWidth = 140, Visibility = Visibility.Collapsed
    };
    private readonly DispatcherTimer _developmentTick = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private bool _developmentTesting;
    private bool _developmentHoldHover;
    private bool DevelopmentOwnsPreview => _developmentTesting || _developmentHoldHover;
    private string _developmentLastReport = "No checks run yet.";
    private TextBlock? _developmentStatus;
    private Button? _developmentRunButton;
    private string DevelopmentDirectory => Path.GetDirectoryName(_settings.FilePath)!;
    private string DevelopmentReportPath => Path.Combine(DevelopmentDirectory, "diagnostics", "ui-checks.json");

    internal void StartDevelopment(string page, bool runChecks)
    {
        Title = "Nagi · Development " + CurrentVersion.ToString(3);
        AutomationProperties.SetAutomationId(_developmentNotification, "DevelopmentNotchNotification");
        RestingActivity.Children.Add(_developmentNotification);
        _developmentTick.Tick += (_, _) =>
        {
            if (_closed) { _developmentTick.Stop(); return; }
            if (DateTimeOffset.UtcNow >= _developmentPreviewUntil)
            {
                _developmentTick.Stop(); _developmentPreview = "";
                if (_developmentHoldHover) { _developmentHoldHover = false; ClosePreview(); }
                RefreshLiveActivity(); UpdateRuntimeDemand(); return;
            }
            if (_developmentPreview == "media")
                _restingVisualizer.Update(.25 + .55 * Math.Abs(Math.Sin(_uptime.Elapsed.TotalSeconds * 3)), true,
                    VisualizerBrush, !ViewModel.Settings.ReducedMotion);
        };
        Closed += (_, _) => _developmentTick.Stop();
        _log.Write("development", "Development build; separate data=" + DevelopmentDirectory);
        OpenDevelopmentPage(page);
        if (runChecks) _ = RunDevelopmentChecksAsync();
    }

    private void OpenDevelopmentPage(string page)
    {
        if (page is "Overview" or "Profiles" or "Utilities" or "Settings") OpenControlCenter(page);
        else { OpenControlCenter("Settings"); ShowSettingsCategory(page); }
    }

    private Button DevelopmentButton(string label, string id, string glyph, RoutedEventHandler action)
    {
        var button = Button(label, action);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        button.Content = row; button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(button, label); AutomationProperties.SetAutomationId(button, id);
        return button;
    }

    private void BuildDevelopmentSettings()
    {
        var intro = Stack(); intro.Children.Add(Heading("Developer tools", "Nagi " + CurrentVersion.ToString(3) + " · Development"));
        intro.Children.Add(Note("F12 or right-click the dock to return here. These tools are excluded from production builds. Development uses its own settings and logs; production updates are disabled."));
        intro.Children.Add(Note("Data: " + DevelopmentDirectory));
        _developmentStatus = Note(_developmentLastReport); _developmentStatus.IsTextSelectionEnabled = true;
        AutomationProperties.SetAutomationId(_developmentStatus, "DevelopmentCheckStatus"); intro.Children.Add(_developmentStatus);
        _developmentRunButton = DevelopmentButton("Run automated UI checks", "DevelopmentRunChecks", "\uE768", async (_, _) => await RunDevelopmentChecksAsync());
        _developmentRunButton.IsEnabled = !_developmentTesting; intro.Children.Add(_developmentRunButton);
        intro.Children.Add(Note("Checks open every page, cycle themes and four screen edges, test timer/media/notification geometry and record results. They restore your development preferences. Existing timers must be stopped first."));
        SettingsPage.Children.Add(Card(intro));

        var pages = Stack(); pages.Children.Add(Heading("Open a page directly"));
        pages.Children.Add(DevelopmentGrid(DevelopmentPages.All.Where(p => p != "Developer tools").Select((page, index) =>
            DevelopmentButton(page, "DevelopmentPage" + index, "\uE8A7", (_, _) => OpenDevelopmentPage(page)))));
        pages.Children.Add(DevelopmentButton("Welcome setup", "DevelopmentWelcome", "\uE77B", (_, _) => RestartWelcome()));
        pages.Children.Add(DevelopmentButton("Dock introduction", "DevelopmentIntroduction", "\uE946", (_, _) => { Collapse(); BeginDockIntroduction(); }));
        SettingsPage.Children.Add(Card(pages));

        var previews = Stack(); previews.Children.Add(Heading("Notch and notification previews"));
        previews.Children.Add(DevelopmentGrid(new[]
        {
            DevelopmentButton("Idle logo", "DevelopmentIdle", "\uE8A7", (_, _) => { ClearDevelopmentPreview(true); ShowDevelopmentPreview("idle"); Collapse(); }),
            DevelopmentButton("10-second timer", "DevelopmentTimer", "\uE916", (_, _) => StartDevelopmentTimer()),
            DevelopmentButton("Timer complete · 00:00", "DevelopmentComplete", "\uE73E", (_, _) => { ShowDevelopmentCompletion(); Collapse(); }),
            DevelopmentButton("Playing music", "DevelopmentMusic", "\uE8D6", (_, _) => { ShowDevelopmentPreview("media"); Collapse(); }),
            DevelopmentButton("Notch notification", "DevelopmentNotification", "\uE7F4", (_, _) => { ViewModel.Publish("Development: test notch notification", true); ShowDevelopmentPreview("notification"); Collapse(); }),
            DevelopmentButton("Real Nagi alert", "DevelopmentRealNotification", "\uE7F4", (_, _) => { ClearDevelopmentPreview(); ShowNotchNotice("Nagi · notification preview"); Collapse(); }),
            DevelopmentButton("Clear preview", "DevelopmentClear", "\uE711", (_, _) => { ClearDevelopmentPreview(true); Collapse(); }),
            DevelopmentButton("Update changelog", "DevelopmentUpdate", "\uE895", async (_, _) => await PreviewDevelopmentUpdateAsync())
        }));
        previews.Children.Add(Note("Music is a silent visual fixture; it does not control your player. Preview content clears after eight seconds. Starting a test timer replaces the current development timer."));
        previews.Children.Add(DevelopmentGrid(new[] { "Top edge", "Bottom edge", "Left edge", "Right edge" }.Select((edge, index) =>
            DevelopmentButton(edge, "DevelopmentEdge" + index, "\uE8A7", async (_, _) => { await SetDevelopmentEdgeAsync(edge); ShowDevelopmentPreview("notification"); Collapse(); }))));
        previews.Children.Add(DevelopmentGrid(new[] { "System", "Akeno", "Dark", "Light" }.Select((theme, index) =>
            DevelopmentButton(theme, "DevelopmentTheme" + index, "\uE790", async (_, _) => { await CommitAsync(s => s with { Theme = theme }); BuildSettingsPage(); }))));
        previews.Children.Add(DevelopmentGrid(new[] { PreviewKind.System, PreviewKind.Media, PreviewKind.Obs, PreviewKind.Apps, PreviewKind.Timer }.Select(kind =>
            DevelopmentButton(kind + " hover panel", "DevelopmentHover" + kind, "\uE8A7", (_, _) =>
            {
                if (kind == PreviewKind.Timer && !HasDockTimer) StartDevelopmentTimer();
                _developmentHoldHover = true; _developmentPreviewUntil = DateTimeOffset.UtcNow.AddSeconds(8); _developmentTick.Start();
                Collapse(); _dock.Hover(); ShowPreview(kind);
            }))));
        SettingsPage.Children.Add(Card(previews));

        var diagnostics = Stack(); diagnostics.Children.Add(Heading("Diagnostics and integrations"));
        diagnostics.Children.Add(Note("Hardware & audio opens live provider status. Integrations contains gaming, FPS and OBS controls. This test runner does not start recording, streaming, external apps, startup registration or update installation."));
        diagnostics.Children.Add(DevelopmentGrid(new[]
        {
            DevelopmentButton("Open integration controls", "DevelopmentIntegrations", "\uE7F4", (_, _) => OpenDevelopmentPage("Integrations")),
            DevelopmentButton("Export state snapshot", "DevelopmentExport", "\uE74E", async (_, _) => await ExportDevelopmentStateAsync()),
            DevelopmentButton("Open application logs", "DevelopmentLogs", "\uE8A5", async (_, _) => await OpenDevelopmentFolderAsync(Path.GetDirectoryName(_log.FilePath)!)),
            DevelopmentButton("Open error log", "DevelopmentErrors", "\uEA39", async (_, _) => await Launcher.LaunchFileAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(_log.ErrorFilePath))),
            DevelopmentButton("Write test error", "DevelopmentTestError", "\uEA39", (_, _) => { _log.Write("error", "Developer test error (intentional)", new InvalidOperationException("Developer log fixture")); SetDevelopmentStatus("Test error written to errors.jsonl."); }),
            DevelopmentButton("Open test reports", "DevelopmentReports", "\uE8A5", async (_, _) => await OpenDevelopmentFolderAsync(Path.GetDirectoryName(DevelopmentReportPath)!))
        }));
        SettingsPage.Children.Add(Card(diagnostics));
    }

    private static Grid DevelopmentGrid(IEnumerable<Button> buttons)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        var index = 0;
        foreach (var button in buttons)
        {
            if (index % 2 == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            Grid.SetRow(button, index / 2); Grid.SetColumn(button, index % 2); grid.Children.Add(button); index++;
        }
        return grid;
    }

    private void SetDevelopmentStatus(string status)
    {
        _developmentLastReport = status;
        if (_developmentStatus is not null) _developmentStatus.Text = status;
    }

    private Task<bool> SetDevelopmentEdgeAsync(string edge) => CommitAsync(s => s with
    { Position = s.Position with { Anchor = edge, Orientation = edge is "Top edge" or "Bottom edge" ? "Horizontal" : "Vertical", OffsetX = 0, OffsetY = 0 } });

    private void ShowDevelopmentPreview(string kind)
    {
        _developmentPreview = kind; _developmentPreviewUntil = DateTimeOffset.UtcNow.AddSeconds(8);
        _developmentTick.Start(); RefreshLiveActivity(); UpdateRuntimeDemand();
    }
    private void ClearDevelopmentPreview(bool cancelTimer = false)
    {
        if (cancelTimer) { _timer.Cancel(); _timerTick.Stop(); UpdateTimerLabels(); }
        _developmentPreview = ""; _developmentPreviewUntil = default; _developmentTick.Stop(); _developmentHoldHover = false;
        _completedUntil = default; RefreshLiveActivity(); UpdateRuntimeDemand();
        UpdateTimerLabels();
    }
    private void StartDevelopmentTimer()
    {
        ClearDevelopmentPreview(); _timerDuration = TimeSpan.FromSeconds(10); _timer.Start(_timerDuration); _timerTick.Start();
        UpdateTimerLabels(); RefreshLiveActivity(); Collapse();
    }
    private void ShowDevelopmentCompletion()
    {
        ClearDevelopmentPreview(true); _completedUntil = DateTimeOffset.UtcNow.AddSeconds(5); _timerTick.Start();
        UpdateTimerLabels(); RestingCountdown.Text = "00:00"; RefreshLiveActivity();
    }
    private async Task PreviewDevelopmentUpdateAsync()
    {
        var dialog = Dialog("Update preview · development fixture", Note("Changelog\n\n• Improved notch layout and animations.\n• Fixed theme switching.\n\nThis preview does not download or install a release."));
        dialog.PrimaryButtonText = "Update now (test)"; dialog.CloseButtonText = "Later";
        dialog.DefaultButton = ContentDialogButton.Close;
        var result = await ShowDialogAsync(dialog);
        _log.Write("development", "Update dialog preview: " + result);
    }
    private async Task OpenDevelopmentFolderAsync(string path)
    {
        Directory.CreateDirectory(path); await Launcher.LaunchFolderPathAsync(path);
    }
    private async Task ExportDevelopmentStateAsync()
    {
        try
        {
            var path = Path.Combine(DevelopmentDirectory, "diagnostics", "state.json"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var process = Process.GetCurrentProcess();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            {
                Channel = "Development", Version = CurrentVersion.ToString(3), Time = DateTimeOffset.UtcNow,
                Page = _page, Category = _settingsCategory, Stage = _dock.Stage.ToString(), Profile = ViewModel.ProfileText,
                Frame = _windowManager.CurrentFrame, Demand = _runtimeDemand, Metrics = ViewModel.Metrics,
                Media = new { ViewModel.Media.Available, ViewModel.Media.Playing, ViewModel.Media.Title, ViewModel.Media.Error },
                Timer = new { _timer.Active, _timer.Running, _timer.Text },
                PrivateBytes = process.PrivateMemorySize64, WorkingSetBytes = process.WorkingSet64, CpuTime = process.TotalProcessorTime,
                ApplicationLog = _log.FilePath, ErrorLog = _log.ErrorFilePath
            }, new JsonSerializerOptions { WriteIndented = true }));
            SetDevelopmentStatus("State snapshot saved: " + path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _log.Write("error", "Development state export failed", ex); SetDevelopmentStatus(ex.Message); }
    }
}
