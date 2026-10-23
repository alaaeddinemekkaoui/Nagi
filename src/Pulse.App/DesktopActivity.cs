using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly DesktopActivityService _desktopActivity = new();
    private readonly List<DesktopNotification> _bannerNotices = [];
    private readonly HashSet<string> _seenBanners = [];
    private bool? _watchingBanners, _watchingTransfers;
    private bool _windowsTransferActive;
    private double _windowsTransferPercent;
    private int _windowsTransferCount;
    private TextBlock? _windowsTransferLabel;
    private Controls.TimerRing? _windowsTransferRing;
    private uint _bannerId;
    private long _desktopActivityGeneration;
    private void InitializeDesktopActivity()
    {
        _desktopActivity.Changed += snapshot => {
          var generation = Interlocked.Read(ref _desktopActivityGeneration);
          DispatcherQueue.TryEnqueue(() => {
            if (_closed) return;
            if (generation != _desktopActivityGeneration) return;
            if (ViewModel.Settings.WindowsNotificationBanners) {
                var visible = snapshot.Banners.Select(b => b.Key).ToHashSet();
                _seenBanners.RemoveWhere(key => !visible.Contains(key));
                DesktopBanner? newest = null;
                foreach (var banner in snapshot.Banners) {
                    if (!_seenBanners.Add(banner.Key)) continue;
                    newest ??= banner;
                    _bannerNotices.Insert(0, new(++_bannerId, banner.App, banner.Title, banner.Body, DateTimeOffset.UtcNow));
                    if (_bannerNotices.Count > 30) _bannerNotices.RemoveAt(30);
                    _desktopNoticeRevision++;
                }
                if (newest is not null && ViewModel.Settings.Notifications && ModePresets.For(ViewModel.Settings.Workspace(ViewModel.Profile)) != "Focus")
                    ShowNotchNotice(newest.App + " · " + newest.Title, external: true);
            }
            var transfers = ViewModel.Settings.WindowsTransfersEnabled ? snapshot.Transfers : [];
            var wasActive = _windowsTransferActive;
            var last = _windowsTransferPercent;
            _windowsTransferCount = transfers.Length; _windowsTransferActive = transfers.Length > 0;
            _windowsTransferPercent = transfers.FirstOrDefault()?.Percent ?? 0;
            if (_windowsTransferActive && !wasActive) { _noticeUntil = default; _notchNotice = ""; _noticeTick.Stop(); }
            if (!_windowsTransferActive && wasActive && ViewModel.Settings.WindowsTransfersEnabled && ViewModel.Settings.Notifications)
                ShowNotchNotice(L.T(last >= 100 ? "Windows file transfer complete" : "Windows file transfer closed"));
            RefreshWindowsTransferCard(); RefreshTransferNotch(); RefreshNotificationList();
          });
        };
        ConfigureDesktopActivity();
    }
    private void ConfigureDesktopActivity()
    {
        var banners = ViewModel.Settings.WindowsNotificationBanners;
        var transfers = ViewModel.Settings.WindowsTransfersEnabled;
        if (_watchingBanners != banners || _watchingTransfers != transfers) {
            Interlocked.Increment(ref _desktopActivityGeneration);
            _watchingBanners = banners; _watchingTransfers = transfers; _desktopActivity.Configure(banners, transfers);
            if (!banners) { _bannerNotices.Clear(); _seenBanners.Clear(); _desktopNoticeRevision++; }
            if (!transfers) { _windowsTransferActive = false; _windowsTransferCount = 0; RefreshTransferNotch(); }
        }
        if (_noticeIsExternal && (!ViewModel.Settings.Notifications || ModePresets.For(ViewModel.Settings.Workspace(ViewModel.Profile)) == "Focus"
            || !banners && !ViewModel.Settings.WindowsNotificationsEnabled)) {
            _noticeUntil = default; _notchNotice = ""; _noticeIsExternal = false; _noticeTick.Stop(); RefreshTransferNotch();
        }
    }
    private void BuildWindowsTransferCard(StackPanel destination)
    {
        var panel = SettingsCardHeader("Windows file transfers", "Copy or move files in File Explorer. Nagi mirrors Windows progress automatically.", "\uE898");
        var toggle = Toggle(L.T("Show Windows file transfers in the notch"), L.T("Read Explorer's progress dialog without copying files through Nagi."),
            ViewModel.Settings.WindowsTransfersEnabled, (settings, enabled) => settings with { WindowsTransfersEnabled = enabled });
        AutomationProperties.SetAutomationId(toggle, "WindowsTransferObservation"); panel.Children.Add(toggle);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _windowsTransferRing = new Controls.TimerRing { Width = 32, Height = 32 };
        _windowsTransferLabel = Note(""); AutomationProperties.SetAutomationId(_windowsTransferLabel, "WindowsTransferStatus");
        row.Children.Add(_windowsTransferRing); row.Children.Add(_windowsTransferLabel); panel.Children.Add(row);
        panel.Children.Add(Note(L.T("Keep the Windows copy/move progress dialog open. Very short transfers, command-line copies and other apps without an accessible progress dialog cannot be tracked.")));
        destination.Children.Add(Card(panel)); RefreshWindowsTransferCard();
    }
    private void RefreshWindowsTransferCard()
    {
        if (_windowsTransferLabel is not null) _windowsTransferLabel.Text = !ViewModel.Settings.WindowsTransfersEnabled ? L.T("Off")
            : _windowsTransferActive ? L.F($"Windows file transfer {_windowsTransferPercent:0}% · {_windowsTransferCount} active") : L.T("Waiting for a Windows file transfer");
        if (_windowsTransferRing is not null) { _windowsTransferRing.Visibility = _windowsTransferActive ? Visibility.Visible : Visibility.Collapsed;
            _windowsTransferRing.Update(_windowsTransferPercent / 100, ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush")); }
    }
}
