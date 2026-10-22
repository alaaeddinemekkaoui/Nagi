using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pulse.Core;
using Pulse.Infrastructure;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly GameSessionClock _gameClock = new();
    private readonly GitHubModeService _githubMode = new();
    private readonly CalendarModeService _calendarMode = new();
    private readonly AudioControlService _creatorMicrophone = new(capture: true);
    private ProtectedSecretStore _githubCredential = null!, _calendarCredential = null!;
    private string _githubToken = "", _calendarSource = "";
    private DeveloperSnapshot _developer = new(null, [], "Connect GitHub in Integrations");
    private CalendarSnapshot _calendar = new(null, "Connect a calendar in Integrations");
    private string _discord = "Checking Discord desktop…";
    private readonly List<Action> _modeUpdates = [];
    private readonly List<ProgressBar> _creatorMeters = [];
    private Task? _modePolling;
    private readonly SemaphoreSlim _modeWake = new(0, 1);
    private sealed record ModeDemand(bool GitHub = false, bool Calendar = false, bool Discord = false);
    private ModeDemand _modeDemand = new();
    private long _githubRefreshAfter, _calendarRefreshAfter;
    private string? _modeUiKey;
    private void InitializeModes()
    {
        var data = Path.GetDirectoryName(_settings.FilePath)!;
        _githubCredential = new(Path.Combine(data, "github.credential")); _githubToken = _githubCredential.Read();
        _calendarCredential = new(Path.Combine(data, "calendar.credential")); _calendarSource = _calendarCredential.Read();
    }
    private async Task PollModesAsync()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            var demand = Volatile.Read(ref _modeDemand); var settings = Volatile.Read(ref _pollSettings);
            try
            {
                if (demand.Discord)
                {
                    var discord = false;
                    foreach (var name in new[] { "Discord", "DiscordCanary", "DiscordPTB" })
                    { var processes = Process.GetProcessesByName(name); discord |= processes.Length > 0; foreach (var process in processes) process.Dispose(); }
                    DispatcherQueue.TryEnqueue(() => { _discord = discord ? "Discord desktop running" : "Discord desktop closed"; RefreshModeValues(); });
                }
                if (demand.GitHub && Environment.TickCount64 >= Volatile.Read(ref _githubRefreshAfter))
                {
                    Volatile.Write(ref _githubRefreshAfter, Environment.TickCount64 + 300000);
                    DeveloperSnapshot result;
                    try { using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(20)); result = await _githubMode.SampleAsync(settings.PinnedRepositories, Volatile.Read(ref _githubToken), deadline.Token); }
                    catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException)
                    { token.ThrowIfCancellationRequested(); result = new(null, [], "GitHub unavailable · check connection and permissions"); }
                    DispatcherQueue.TryEnqueue(() => { if (!_closed && ViewModel.Settings.GitHubEnabled) { _developer = result; RebuildModePanels(); } });
                }
                if (demand.Calendar && Environment.TickCount64 >= Volatile.Read(ref _calendarRefreshAfter))
                {
                    Volatile.Write(ref _calendarRefreshAfter, Environment.TickCount64 + 300000);
                    CalendarSnapshot result;
                    try { result = await _calendarMode.SampleAsync(Volatile.Read(ref _calendarSource), DateTimeOffset.UtcNow, token); }
                    catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OperationCanceledException)
                    { token.ThrowIfCancellationRequested(); result = new(null, "Calendar unavailable · check the source in Integrations"); }
                    DispatcherQueue.TryEnqueue(() => { if (!_closed && ViewModel.Settings.CalendarEnabled) { _calendar = result; RebuildModePanels(); } });
                }
                await _modeWake.WaitAsync(demand.Discord ? 5000 : 15000, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.Write("modes", "Mode provider unavailable", ex); try { await Task.Delay(5000, token); } catch (OperationCanceledException) { break; } }
        }
    }
    private void UpdateModeDemand(bool visible)
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile); var mode = ModePresets.For(workspace);
        var connections = !_userHidden && _windowManager.IsVisible && _controlCenter && _page == "Settings" && _settingsCategory == "Integrations";
        var next = new ModeDemand((visible && workspace.ModeEnabled && mode == "Developer" || connections) && ViewModel.Settings.GitHubEnabled,
            (visible && workspace.ModeEnabled && mode == "Focus" || connections) && ViewModel.Settings.CalendarEnabled,
            visible && workspace.ModeEnabled && mode == "Gaming");
        if (next == Volatile.Read(ref _modeDemand)) return;
        Volatile.Write(ref _modeDemand, next); Wake(_modeWake);
    }
    private void RefreshModeValues()
    {
        if (_closed) return;
        foreach (var update in _modeUpdates) update();
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        var mode = ModePresets.For(workspace);
        RailModeValue.Text = mode switch { "Gaming" => CountdownTimer.Format(_gameClock.Elapsed), "Developer" => _developer.Unread?.ToString() ?? "—", "Creator" => _obsSnapshot.Recording ? L.T("REC") : _obsSnapshot.Streaming ? L.T("LIVE") : L.T("Studio"), "Focus" => L.T("Task"), _ => "" };
        RailModeIcon.Glyph = ModeGlyph(mode);
    }
    private static string ModeGlyph(string mode) => mode switch { "Gaming" => "\uE7FC", "Developer" => "\uE943", "Creator" => "\uE714", "Focus" => "\uE708", _ => "\uE80F" };
    private void RebuildModePanels()
    {
        if (_closed) return;
        _modeUiKey = null; BuildModePanels();
    }
    private void BuildModePanels()
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile); var mode = ModePresets.For(workspace);
        var key = $"{ViewModel.Profile}:{mode}:{workspace.ModeEnabled}:{workspace.FocusTask}:{ViewModel.Settings.GitHubEnabled}:{ViewModel.Settings.CalendarEnabled}";
        if (_modeUiKey == key) { RefreshModeValues(); return; } _modeUiKey = key;
        _modeUpdates.Clear(); _creatorMeters.Clear(); ModeOverview.Children.Clear(); ModePeek.Children.Clear();
        ModeOverview.Visibility = workspace.ModeEnabled && mode != "Default" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var parent in new[] { ModeOverview, ModePeek })
        {
            var panel = SettingsCardHeader(mode + " mode", ModePresets.Description(mode), ModeGlyph(mode));
            AutomationProperties.SetAutomationId(panel, parent == ModeOverview ? "ActiveModeCard" : "ModeHoverCard");
            void Value(string title, Func<string> read, string id)
            {
                panel.Children.Add(Note(L.T(title))); var text = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                AutomationProperties.SetAutomationId(text, id + (parent == ModePeek ? "Peek" : "")); panel.Children.Add(text);
                void Update() { var value = read(); if (text.Text != value) text.Text = value; } Update(); _modeUpdates.Add(Update);
            }
            if (mode == "Gaming")
            {
                Value("Active game time", () => _gameClock.Game.Length == 0 ? L.T("Waiting for a recognized game") : _gameClock.Game + " · " + CountdownTimer.Format(_gameClock.Elapsed), "GameSessionTime");
                Value("FPS", () => CurrentFps is { } f ? L.F($"{f:0} FPS") : ViewModel.Settings.FpsEnabled ? _fpsSnapshot.Status : L.T("Enable PresentMon FPS in Integrations"), "GamingFps");
                Value("Temperatures", () => _temperatureSnapshot.Readings.Length == 0 ? L.T("Connect a supported temperature sensor provider") : string.Join(" · ", _temperatureSnapshot.Readings.Take(3).Select(r => L.F($"{r.Name} {r.Celsius:0}°C"))), "GamingTemperatures");
                Value("Discord", () => L.T(_discord), "GamingDiscord");
                panel.Children.Add(Note(L.T("Discord status describes the desktop app, not private presence or voice activity.")));
                panel.Children.Add(Button(L.T("Gaming integrations"), (_, _) => OpenUtilitySettings("Integrations")));
            }
            else if (mode == "Developer")
            {
                Value("GitHub notifications", () => !ViewModel.Settings.GitHubEnabled ? L.T("Connect GitHub in Integrations") : _developer.Unread is { } count ? count >= 50 ? L.T("50+ unread") : L.F($"{count} unread") : L.T(_developer.Status), "DeveloperNotifications");
                panel.Children.Add(new HyperlinkButton { Content = L.T("Open GitHub notifications"), NavigateUri = new("https://github.com/notifications"), Padding = new(0) });
                foreach (var build in (ViewModel.Settings.GitHubEnabled ? _developer.Repositories : []).Take(parent == ModePeek ? 2 : 6))
                {
                    var row = Stack(); row.Spacing = 3; row.Children.Add(Heading(build.Repository)); row.Children.Add(Note(L.T(build.Status)));
                    row.Children.Add(new HyperlinkButton { Content = L.T("Open repository / build"), NavigateUri = new(build.Url ?? "https://github.com/" + build.Repository), Padding = new(0) }); panel.Children.Add(Card(row));
                }
                if (_developer.Repositories.Length == 0) panel.Children.Add(Note(L.T("Pin repositories in Integrations to see real GitHub Actions build status.")));
                panel.Children.Add(Button(L.T("Developer integrations"), (_, _) => OpenUtilitySettings("Integrations")));
            }
            else if (mode == "Creator")
            {
                Value("OBS Studio", () => !_obsSnapshot.Connected ? _obsSnapshot.Status : L.T(_obsSnapshot.Recording ? "Recording" : "Not recording") + " · " + L.T(_obsSnapshot.Streaming ? "Live" : "Not streaming"), "CreatorObs");
                Value("Now playing", () => ViewModel.MediaTitle, "CreatorMedia");
                panel.Children.Add(Note(L.T("Live microphone level"))); var meter = new ProgressBar { Minimum = 0, Maximum = 100, Height = 8, IsIndeterminate = false };
                AutomationProperties.SetName(meter, L.T("Live microphone level")); _creatorMeters.Add(meter); panel.Children.Add(meter);
                panel.Children.Add(Button(L.T("Toggle microphone mute"), Microphone_Click));
                panel.Children.Add(Button(L.T("OBS controls"), (_, _) => { if (!_controlCenter) OpenControlCenter(); IntegrationCard.StartBringIntoView(); }));
            }
            else if (mode == "Focus")
            {
                Value("Current task", () => workspace.FocusTask.Length > 0 ? workspace.FocusTask : L.T("Choose one thing to focus on"), "FocusTaskValue");
                Value("Timer", () => _timer.Active ? _timer.Text : CountdownTimer.Format(TimeSpan.FromMinutes(ViewModel.Settings.TimerFor(ViewModel.Profile).Minutes)), "FocusModeTimer");
                panel.Children.Add(Button(L.T("Start timer"), TimerStart_Click));
                Value("Next meeting", () => !ViewModel.Settings.CalendarEnabled ? L.T("Connect a calendar in Integrations") : _calendar.Next is { } meeting && meeting.End > DateTimeOffset.UtcNow
                    ? meeting.Title + " · " + meeting.Start.ToLocalTime().ToString("ddd HH:mm") : L.T("No upcoming meeting"), "FocusMeeting");
                if (_calendar.Next?.JoinUrl is { } url) panel.Children.Add(new HyperlinkButton { Content = L.T("Join meeting"), NavigateUri = new(url), Padding = new(0) });
                if (ViewModel.Settings.CalendarEnabled) panel.Children.Add(Note(L.T(_calendar.Status)));
                panel.Children.Add(Note(L.T("Only important Nagi notices appear. Other apps' Windows notifications are unchanged.")));
                panel.Children.Add(Button(L.T("Edit task & mode"), (_, _) => { _editingWorkspace = ViewModel.Profile; OpenControlCenter("Profiles"); }));
            }
            parent.Children.Add(Card(panel));
        }
        RefreshModeValues();
    }
    private void ModePreview_Entered(object sender, PointerRoutedEventArgs args) => QueuePreview(PreviewKind.Mode, RailModeButton);
    private void Mode_Click(object sender, RoutedEventArgs args) => OpenControlCenter("Overview");
}
