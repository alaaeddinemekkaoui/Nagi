using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private string? _integrationPage;
    private bool _fpsPermissionNoticeSent;
    private bool FpsNeedsPermission => ViewModel.Settings.FpsEnabled &&
        (_fpsSnapshot.Status.Contains("denied", StringComparison.OrdinalIgnoreCase)
        || _fpsSnapshot.Status.Contains("permission", StringComparison.OrdinalIgnoreCase));

    private void OpenIntegration(string name)
    {
        _settingsCategory = "Integrations"; _integrationPage = name;
        BuildSettingsPage(); ContentScroll.ChangeView(null, 0, null, true); Animate(SettingsPage);
    }

    private void RefreshIntegrationNotices()
    {
        if (!FpsNeedsPermission) { _fpsPermissionNoticeSent = false; return; }
        if (_fpsPermissionNoticeSent) return;
        _fpsPermissionNoticeSent = true;
        ViewModel.Publish(L.T("FPS tracing needs permission. Open Settings → Integrations → PresentMon FPS to start the helper."), true);
    }

    private void AddIntegrationSetupNotice()
    {
        if (!FpsNeedsPermission) return;
        var action = Button(L.T("Review setup"), (_, _) => OpenIntegration("PresentMon FPS"));
        AutomationProperties.SetAutomationId(action, "ReviewFpsSetup");
        SettingsPage.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning,
            Title = L.T("FPS tracing needs permission"), Message = L.T("Start the FPS helper to allow Windows frame timing capture."), ActionButton = action });
    }

    private string IntegrationStatus(string name) => name switch
    {
        "Gaming" => ViewModel.Settings.AutomaticGameDetection ? L.T("Automatic detection on") : L.T("Off"),
        "PresentMon FPS" => FpsNeedsPermission ? L.T("Needs permission") : CurrentFps is { } fps ? L.F($"{fps:0} FPS")
            : ViewModel.Settings.FpsEnabled ? L.T("Waiting for a graphics app") : L.T("Off"),
        "GitHub" => !ViewModel.Settings.GitHubEnabled ? L.T("Off") : _githubCredential.Exists || ViewModel.Settings.PinnedRepositories.Length > 0 ? L.T("Connected") : L.T("Needs setup"),
        "Calendar" => !ViewModel.Settings.CalendarEnabled ? L.T("Off") : _calendarCredential.Exists ? L.T("Connected") : L.T("Needs setup"),
        "OBS Studio" => !ViewModel.Settings.ObsEnabled ? L.T("Off") : _obsSnapshot.Connected ? L.T("Connected") : L.T("Check connection"),
        "Notifications" => ViewModel.Settings.WindowsNotificationBanners || ViewModel.Settings.WindowsNotificationsEnabled && _windowsNotices.Allowed ? L.T("Connected") : L.T("Needs setup"),
        _ => ""
    };

    private void BuildIntegrationList()
    {
        AddIntegrationSetupNotice();
        var entries = new (string Name, string Detail, string Glyph)[] {
            ("Gaming", "Game detection and automatic profiles", "\uE7FC"),
            ("PresentMon FPS", "Frame timing and tracing permission", "\uE9D9"),
            ("GitHub", "Notifications and repository builds", "\uE943"),
            ("Calendar", "Upcoming meetings from a link or .ics file", "\uE787"),
            ("OBS Studio", "Recording, streaming and scenes", "\uE714"),
            ("Notifications", "Nagi alerts and other Windows apps", "\uE7F4") };
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        var columns = 0;
        void Arrange(int count)
        {
            if (columns == count) return; columns = count;
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for (var i = 0; i < count; i++) grid.ColumnDefinitions.Add(new());
            for (var i = 0; i < (entries.Length + count - 1) / count; i++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (var i = 0; i < grid.Children.Count; i++) { Grid.SetRow((FrameworkElement)grid.Children[i], i / count); Grid.SetColumn((FrameworkElement)grid.Children[i], i % count); }
        }
        foreach (var entry in entries)
        {
            var panel = SettingsCardHeader(entry.Name, entry.Detail, entry.Glyph);
            var status = Note(IntegrationStatus(entry.Name)); panel.Children.Add(status);
            _settingsCardUpdates.Add(() => { status.Text = IntegrationStatus(entry.Name); });
            var tile = new Button { Content = panel, Padding = new(14), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new(16), BorderThickness = new(0), Background = ThemeBrush("SurfaceBrush") };
            AutomationProperties.SetName(tile, L.T(entry.Name));
            AutomationProperties.SetAutomationId(tile, "Integration" + string.Concat(entry.Name.Where(char.IsLetterOrDigit)));
            tile.Click += (_, _) => OpenIntegration(entry.Name); _motion.Wire(tile); grid.Children.Add(tile);
        }
        Arrange(3); grid.SizeChanged += (_, args) => Arrange(args.NewSize.Width < 420 ? 1 : args.NewSize.Width < 680 ? 2 : 3);
        SettingsPage.Children.Add(grid);
    }
}
