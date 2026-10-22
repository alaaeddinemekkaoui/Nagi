using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly List<Action> _settingsCardUpdates = [];
    private void RefreshSettingsCards()
    {
        if (_controlCenter && _page == "Settings") foreach (var update in _settingsCardUpdates) update();
    }

    private StackPanel SettingsCardHeader(string title, string description, string glyph)
    {
        var panel = Stack();
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new());
        row.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new(12), Background = ThemeBrush("AccentWashBrush"),
            Child = new FontIcon { Glyph = glyph, FontSize = 18, Foreground = ThemeBrush("PulseAccentBrush") } });
        var text = Stack(); text.Spacing = 3;
        text.Children.Add(Heading(L.T(title))); if (description.Length > 0) text.Children.Add(Note(L.T(description)));
        Grid.SetColumn(text, 1); row.Children.Add(text); panel.Children.Add(row);
        return panel;
    }

    private Border SettingsStatusCard(string title, string glyph, Func<(string Value, string Detail)> read, string id)
    {
        var panel = SettingsCardHeader(title, "", glyph);
        var value = new TextBlock { FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        var detail = Note(""); panel.Children.Add(value); panel.Children.Add(detail);
        AutomationProperties.SetAutomationId(value, id);
        void Update() { var next = read(); if (value.Text != next.Value) value.Text = next.Value; if (detail.Text != next.Detail) detail.Text = next.Detail; }
        Update(); _settingsCardUpdates.Add(Update);
        return Card(panel);
    }

    private static Grid SettingsCardGrid(params FrameworkElement[] cards)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        var columns = 0;
        void Arrange(int count)
        {
            if (count == columns) return; columns = count;
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for (var i = 0; i < count; i++) grid.ColumnDefinitions.Add(new());
            for (var i = 0; i < (cards.Length + count - 1) / count; i++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (var i = 0; i < cards.Length; i++) { Grid.SetRow(cards[i], i / count); Grid.SetColumn(cards[i], i % count); }
        }
        foreach (var card in cards) grid.Children.Add(card);
        Arrange(2); grid.SizeChanged += (_, args) => Arrange(args.NewSize.Width < 640 ? 1 : 2);
        return grid;
    }

    private void BuildHardwareAudioSettings()
    {
        SettingsPage.Children.Add(SettingsCardGrid(
            SettingsStatusCard("CPU & memory", "\uE950", () => (ViewModel.CpuText,
                ViewModel.Metrics.TotalMemory > 0 ? L.F($"{GiB(ViewModel.Metrics.AvailableMemory):0.0} GB available of {GiB(ViewModel.Metrics.TotalMemory):0.0} GB") : L.T("waiting / unavailable")), "HardwareCpuStatus"),
            SettingsStatusCard("Graphics", "\uE7F4", () => (_gpuSnapshot.Usage is { } usage ? L.F($"{usage:0}%") : "—", _gpuSnapshot.Name + " · " + _gpuSnapshot.Status), "HardwareGpuStatus")));
        var output = SettingsCardHeader("Speakers & headphones", "Choose an output or adjust your listening volume.", "\uE767");
        var device = Note(ViewModel.Audio.Device); output.Children.Add(device);
        var volumeLabel = Note(""); output.Children.Add(volumeLabel);
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = ViewModel.Audio.Volume ?? 0, IsEnabled = ViewModel.Audio.Volume.HasValue };
        AutomationProperties.SetName(volume, L.T("Output volume")); AutomationProperties.SetAutomationId(volume, "SettingsOutputVolume");
        var changing = false; var interacting = false;
        volume.PointerPressed += (_, _) => interacting = true;
        volume.PointerReleased += (_, _) => interacting = false;
        volume.PointerCaptureLost += (_, _) => interacting = false;
        volume.ValueChanged += (_, args) => { if (!changing) Volume_Changed(volume, args); };
        _settingsCardUpdates.Add(() => { device.Text = ViewModel.Audio.Device; volumeLabel.Text = ViewModel.Audio.Volume is { } value ? ViewModel.Audio.Muted ? L.T("Muted") : L.F($"{value:0}%") : L.T("Unavailable"); volume.IsEnabled = ViewModel.Audio.Volume.HasValue; if (!interacting && volume.FocusState != FocusState.Keyboard && !_volumeTimer.IsEnabled && !_volumeWriteInFlight) { changing = true; volume.Value = ViewModel.Audio.Volume ?? 0; changing = false; } });
        output.Children.Add(volume);
        output.Children.Add(Button(L.T("Choose speaker"), Speakers_Click));
        output.Children.Add(Button(L.T("Mute / unmute output"), Mute_Click));
        var microphone = SettingsCardHeader("Microphone", "Control the default communications microphone. Nagi does not record audio.", "\uE720");
        var micStatus = Note(""); microphone.Children.Add(micStatus);
        var mic = Button(L.T("Toggle microphone mute"), Microphone_Click);
        void UpdateMic() { micStatus.Text = _microphoneSnapshot.Device; mic.IsEnabled = _microphoneSnapshot.Volume.HasValue; mic.Content = L.T(_microphoneSnapshot.Muted ? "Unmute microphone" : "Mute microphone"); }
        UpdateMic(); _settingsCardUpdates.Add(UpdateMic); microphone.Children.Add(mic);
        SettingsPage.Children.Add(SettingsCardGrid(Card(output), Card(microphone)));
        var temperatures = SettingsCardHeader("Temperature sensors", "Connect Libre Hardware Monitor or Open Hardware Monitor with WMI enabled. Nagi does not install a kernel driver.", "\uE9CA");
        var sensors = Note(""); temperatures.Children.Add(sensors);
        void UpdateSensors() => sensors.Text = _temperatureSnapshot.Readings.Length > 0
            ? string.Join(" · ", _temperatureSnapshot.Readings.Select(x => L.F($"{x.Name} {x.Celsius:0}°C"))) : _temperatureSnapshot.Status;
        UpdateSensors(); _settingsCardUpdates.Add(UpdateSensors); SettingsPage.Children.Add(Card(temperatures));
        SettingsPage.Children.Add(Button(L.T("Gaming & OBS integrations"), (_, _) => ShowSettingsCategory("Integrations")));
        RefreshSettingsCards();
    }

    private void BuildFriendlyDiagnosticsSettings()
    {
        var about = SettingsCardHeader("Nagi", "A little space. More control.", "\uE946");
        var version = new TextBlock { Text = "v" + CurrentVersion.ToString(3), FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        AutomationProperties.SetAutomationId(version, "AboutVersion"); about.Children.Add(version);
        about.Children.Add(Note(L.T("Your calm desktop companion. Built by Alaa Eddine Mekkaoui, with a little room for silly.")));
        about.Children.Add(new HyperlinkButton { Content = L.T("Project on GitHub"), NavigateUri = new("https://github.com/alaaeddinemekkaoui/Nagi"), Padding = new(0) });
        var privacy = SettingsCardHeader("Yours, locally", "No account. No activity telemetry.", "\uE72E");
        privacy.Children.Add(Note(L.T("Settings and logs stay on your PC. Weather, updates and enabled integrations connect only for their features.")));
        privacy.Children.Add(Button(L.T("Open Nagi data folder"), (_, _) => Report(ActionLauncher.Launch(new("Nagi data", ActionKind.Folder, Path.GetDirectoryName(_settings.FilePath)!)))));
        SettingsPage.Children.Add(SettingsCardGrid(Card(about), Card(privacy)));
        SettingsPage.Children.Add(Heading(L.T("At a glance")));
        SettingsPage.Children.Add(SettingsCardGrid(
            SettingsStatusCard("Session", "\uE916", () => (_uptime.Elapsed.ToString(@"hh\:mm\:ss"), L.F($"Nagi {CurrentVersion.ToString(3)} · .NET {Environment.Version}")), "DiagnosticsSession"),
            SettingsStatusCard("Active application", "\uE737", () => (ViewModel.Foreground.Name, ViewModel.ContextText + L.F($" · PID {ViewModel.Foreground.ProcessId}")), "DiagnosticsForeground"),
            SettingsStatusCard("Media & audio", "\uE8D6", () => (ViewModel.Media.Available ? L.T(ViewModel.Media.Playing ? "Playing" : "Paused") : L.T("Nothing playing"), ViewModel.Audio.Device), "DiagnosticsAudio"),
            SettingsStatusCard("Desktop controls", "\uE765", () => (_tray?.IsRegistered == true && _hotkeyAvailable && _hideHotkeyAvailable && _searchHotkeyAvailable ? L.T("Ready") : L.T("Needs attention"),
                L.T(_tray?.IsRegistered == true ? "System tray connected" : "System tray unavailable") + " · " + L.T(_hotkeyAvailable && _hideHotkeyAvailable && _searchHotkeyAvailable ? "Shortcuts registered" : "A shortcut is unavailable")), "DiagnosticsControls")));
        var logs = SettingsCardHeader("Logs & troubleshooting", "Open a log when something needs a closer look.", "\uE9D9");
        logs.Children.Add(Button(L.T("Open activity log"), (_, _) => Report(ActionLauncher.Launch(new("Activity log", ActionKind.File, _log.FilePath)))));
        logs.Children.Add(Button(L.T("Open error log"), (_, _) => Report(ActionLauncher.Launch(new("Error log", ActionKind.File, _log.ErrorFilePath)))));
        var details = Stack();
        details.Children.Add(Note(L.T("Settings: ") + _settings.FilePath)); details.Children.Add(Note(L.T("Activity log: ") + _log.FilePath)); details.Children.Add(Note(L.T("Error log: ") + _log.ErrorFilePath));
        var runtime = Note(""); details.Children.Add(runtime);
        _settingsCardUpdates.Add(() => runtime.Text = _glass.MaterialStatus + "\n" + L.F($"Audio refresh: {Volatile.Read(ref _runtimeDemand).AudioIntervalMs} ms · Frames: {_audioFrameCount}") + "\n" + _fpsSnapshot.Status + "\n" + _obsSnapshot.Status);
        logs.Children.Add(new Expander { Header = L.T("Technical details"), Content = details, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        logs.Children.Add(Button(L.T("Hardware & audio"), (_, _) => ShowSettingsCategory("Hardware & audio")));
        SettingsPage.Children.Add(Card(logs));
        var activity = SettingsCardHeader("Recent activity", "Profile changes and notices from this session.", "\uE81C");
        if (ViewModel.Activity.Count == 0) activity.Children.Add(Note(L.T("No profile changes yet.")));
        foreach (var entry in ViewModel.Activity.Take(6))
        {
            var row = Stack(); row.Spacing = 3; row.Children.Add(Note(entry.Time.ToString("HH:mm:ss"))); row.Children.Add(new TextBlock { Text = entry.Message, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            activity.Children.Add(new Border { Background = ThemeBrush("InsetBrush"), CornerRadius = new(10), Padding = new(12), Child = row });
        }
        SettingsPage.Children.Add(Card(activity));
        var maintenance = SettingsCardHeader("App controls", "Restore your preferences or close Nagi.", "\uE713");
        maintenance.Children.Add(Button(L.T("Restore defaults"), async (_, _) => await RestoreDefaultsAsync()));
        maintenance.Children.Add(Button(L.T("Quit Nagi"), (_, _) => Close())); SettingsPage.Children.Add(Card(maintenance));
        // Community credits belong at the end, after the useful app information.
        var thanks = SettingsCardHeader("Thanks", "Thank you for the suggestions to improve Nagi.", "\uEB51");
        foreach (var credit in new[] { ("alkidnn", "AboutRedditCredit", "Suggestions and feedback that helped shape Nagi."), ("will25u1", "AboutRedditSearchCredit", "Mini bar testing, hover reports and search suggestions.") })
        {
            var row = Stack(); row.Spacing = 4;
            var link = new HyperlinkButton { Content = "u/" + credit.Item1 + " · Reddit", NavigateUri = new("https://www.reddit.com/user/" + credit.Item1 + "/"), Padding = new(0) };
            AutomationProperties.SetAutomationId(link, credit.Item2); row.Children.Add(link); row.Children.Add(Note(L.T(credit.Item3)));
            thanks.Children.Add(new Border { Background = ThemeBrush("InsetBrush"), CornerRadius = new(12), Padding = new(14), Child = row });
        }
        AutomationProperties.SetAutomationId(thanks, "AboutAcknowledgment"); SettingsPage.Children.Add(Card(thanks));
        RefreshSettingsCards();
    }
}
