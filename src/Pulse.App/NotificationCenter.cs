using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly WindowsNotificationService _windowsNotices = new();
    private DesktopNotification[] _desktopNotices = [];
    private readonly HashSet<uint> _seenDesktopNotices = [];
    private bool _notificationReadBusy, _notificationBaseline;
    private long _nextNotificationRead;
    private StackPanel? _notificationList;
    private string? _notificationUiKey;
    private string _desktopSnapshotKey = "";
    private int _desktopNoticeRevision;
    private readonly Controls.TimerRing _notchStatusRing = new() { Width = 26, Height = 26, Visibility = Visibility.Collapsed };
    private readonly TextBlock _notchStatusText = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 140, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly DispatcherTimer _noticeTick = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTimeOffset _noticeUntil;
    private string _notchNotice = "";
    private bool _updateTransferBusy;
    private double _updateTransferPercent;
    private string NotificationActivity => DateTimeOffset.UtcNow < _noticeUntil ? "notice" : _fileTransferBusy || _updateTransferBusy ? "transfer" : "";

    private void InitializeNotifications()
    {
        RestingActivity.Children.Add(_notchStatusRing); RestingActivity.Children.Add(_notchStatusText);
        AutomationProperties.SetAutomationId(_notchStatusText, "NotchNotificationText");
        ViewModel.NoticePublished += (message, essential) => { if (ViewModel.Settings.Notifications || essential) ShowNotchNotice(message); RefreshNotificationList(); };
        _noticeTick.Tick += (_, _) => { if (DateTimeOffset.UtcNow >= _noticeUntil) { _noticeTick.Stop(); _notchNotice = ""; } RefreshTransferNotch(); };
        _windowsNotices.Changed += WindowsNoticesChanged;
        if (ViewModel.Settings.WindowsNotificationsEnabled && _windowsNotices.Allowed) { _windowsNotices.Listen(); _ = ReadWindowsNoticesAsync(); }
    }
    private void ShowNotchNotice(string message)
    {
        if (_closed) return;
        _notchNotice = message; _noticeUntil = DateTimeOffset.UtcNow.AddSeconds(6); _noticeTick.Start();
        _restingActivity = ""; RefreshTransferNotch();
    }
    private void RefreshTransferNotch()
    {
        var activity = NotificationActivity;
        var progress = activity == "notice" ? 1 - Math.Clamp((_noticeUntil - DateTimeOffset.UtcNow).TotalSeconds / 6, 0, 1)
            : (_fileTransferBusy ? _fileTransferProgress : _updateTransferPercent) / 100;
        _notchStatusRing.Update(progress, ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush"));
        _notchStatusText.Text = activity == "notice" ? _notchNotice : L.F($"{progress * 100:0}%");
        RefreshLiveActivity();
    }
    private void SetUpdateTransfer(double percent, bool busy)
    { _updateTransferPercent = percent; _updateTransferBusy = busy; RefreshTransferProgress(); }
    private void WindowsNoticesChanged() => DispatcherQueue.TryEnqueue(async () => { if (!_closed) await ReadWindowsNoticesAsync(); });
    private async Task ReadWindowsNoticesAsync()
    {
        if (_notificationReadBusy || !ViewModel.Settings.WindowsNotificationsEnabled) return;
        _notificationReadBusy = true; _nextNotificationRead = Environment.TickCount64 + 15000;
        try
        {
            var items = await _windowsNotices.ReadAsync(); if (_closed || !ViewModel.Settings.WindowsNotificationsEnabled) return;
            var snapshotKey = string.Join("|", items.Select(n => n.Id + n.Title + n.Body));
            if (snapshotKey != _desktopSnapshotKey) { _desktopSnapshotKey = snapshotKey; _desktopNoticeRevision++; }
            var newest = _notificationBaseline ? items.FirstOrDefault(n => !_seenDesktopNotices.Contains(n.Id)) : null;
            _desktopNotices = items; _seenDesktopNotices.Clear(); foreach (var item in items) _seenDesktopNotices.Add(item.Id); _notificationBaseline = true;
            if (newest is not null && ViewModel.Settings.Notifications && Pulse.Core.ModePresets.For(ViewModel.Settings.Workspace(ViewModel.Profile)) != "Focus") ShowNotchNotice(newest.App + " · " + newest.Title);
            RefreshNotificationList();
        }
        catch (Exception ex) { _log.Write("notifications", "Windows notification refresh failed: " + ex.GetType().Name); }
        finally { _notificationReadBusy = false; }
    }
    private void BuildNotificationCenter()
    {
        var panel = SettingsCardHeader("Notifications", "Nagi alerts and notifications shared by Windows.", "\uE7F4");
        AutomationProperties.SetAutomationId(panel, "NotificationCenter");
        panel.Children.Add(Button(L.T("Notification settings"), (_, _) => OpenIntegrationFromUtilities("Notifications")));
        panel.Children.Add(Button(L.T("Clear Nagi alerts"), (_, _) => { ViewModel.Activity.Clear(); RefreshNotificationList(); }));
        _notificationList = Stack(); panel.Children.Add(_notificationList); UtilitiesPage.Children.Add(Card(panel)); _notificationUiKey = null; RefreshNotificationList();
    }
    private void OpenIntegrationFromUtilities(string name) { OpenControlCenter("Settings"); OpenIntegration(name); }
    private void RefreshNotificationList()
    {
        if (_notificationList is null || _page != "Utilities") return;
        var key = ViewModel.Activity.Count + ":" + ViewModel.Activity.FirstOrDefault()?.Time.ToString("O") + ":" + _desktopNoticeRevision + _windowsNotices.Status;
        if (key == _notificationUiKey) return; _notificationUiKey = key; _notificationList.Children.Clear();
        foreach (var alert in ViewModel.Activity.Take(8)) _notificationList.Children.Add(NotificationCard("Nagi", alert.Message, "", alert.Time));
        foreach (var alert in _desktopNotices) _notificationList.Children.Add(NotificationCard(alert.App, alert.Title, alert.Body, alert.Created));
        if (ViewModel.Activity.Count == 0 && _desktopNotices.Length == 0) _notificationList.Children.Add(Note(L.T("All quiet. Your notifications will appear here.")));
        if (ViewModel.Settings.WindowsNotificationsEnabled) _notificationList.Children.Add(Note(L.T(_windowsNotices.Status)));
    }
    private Border NotificationCard(string source, string title, string body, DateTimeOffset created)
    {
        var panel = SettingsCardHeader(source, created.ToLocalTime().ToString("g"), "\uE7F4");
        panel.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (body.Length > 0) panel.Children.Add(Note(body)); return Card(panel);
    }
    private void BuildWindowsNotificationSettings()
    {
        var panel = SettingsCardHeader("Windows notifications", "Read other apps' notifications after Windows grants access.", "\uE7F4");
        panel.Children.Add(Note(L.T("Notification contents stay in memory. Nagi does not upload them, log them or dismiss them from Windows. Focus mode suppresses external notification previews.")));
        if (!WindowsNotificationService.HasIdentity) panel.Children.Add(Note(L.T("Other apps' notifications require the packaged Nagi edition and Windows permission. Portable builds still show Nagi alerts and transfers.")));
        panel.Children.Add(Note(L.T(_windowsNotices.Status)));
        var connect = Button(L.T("Connect Windows notifications"), async (_, _) =>
        {
            if (await _windowsNotices.RequestAccessAsync()) { if (await CommitAsync(s => s with { WindowsNotificationsEnabled = true })) { _windowsNotices.Listen(); _notificationBaseline = false; await ReadWindowsNoticesAsync(); } }
            else ViewModel.Publish(L.T(_windowsNotices.Status), true);
            if (!_closed) BuildSettingsPage();
        }); AutomationProperties.SetAutomationId(connect, "ConnectWindowsNotifications"); panel.Children.Add(connect);
        panel.Children.Add(Button(L.T("Disconnect Windows notifications"), async (_, _) => {
            if (!await CommitAsync(s => s with { WindowsNotificationsEnabled = false })) return;
            _windowsNotices.Stop(); _desktopNotices = []; _desktopSnapshotKey = ""; _desktopNoticeRevision++;
            _seenDesktopNotices.Clear(); _notificationBaseline = false; _notificationUiKey = null;
            _noticeUntil = default; _notchNotice = ""; _noticeTick.Stop(); RefreshTransferNotch(); BuildSettingsPage();
        }));
        panel.Children.Add(Button(L.T("Open Windows notification privacy"), (_, _) => { try { using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-notifications") { UseShellExecute = true }); } catch (Exception ex) { _log.Write("notifications", "Could not open Windows settings", ex); } }));
        SettingsPage.Children.Add(Card(panel));
    }
}
