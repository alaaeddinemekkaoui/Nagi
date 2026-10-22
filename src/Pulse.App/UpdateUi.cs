using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Infrastructure;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private string _updateStatus = L.T("Check for a new release.");
    private static Version CurrentVersion => typeof(MainWindow).Assembly.GetName().Version ?? new(0, 1, 2, 0);
#if !NAGI_DEVELOPMENT
    private static readonly HttpClient UpdateClient = new() { Timeout = TimeSpan.FromMinutes(15) };
    private readonly AppUpdateService _updates = new(UpdateClient);
    private AppRelease? _availableUpdate;
    private PreparedUpdate? _preparedUpdate;
    private bool _updateBusy;
    private Version? _offeredUpdateVersion;
    private TextBlock? _updateStatusText;
    private TextBlock? _updateNotesText;
    private Controls.TimerRing? _updateProgressRing;
    private TextBlock? _updateProgressLabel;
    private Grid? _updateProgressView;
    private double _updateProgress = -1;
    private Button? _checkUpdateButton, _installUpdateButton;
    private string UpdateResultPath => Path.Combine(Path.GetDirectoryName(_settings.FilePath)!, "update-result.json");
#endif
    private void InitializeUpdates()
    {
#if NAGI_DEVELOPMENT
        _updateStatus = "Development build: use a new development executable to update. Production updates are disabled.";
#else
        if (Pulse.Windows.WindowsNotificationService.HasIdentity) { _updateStatus = L.T("Packaged edition: install a newer signed MSIX from GitHub releases."); return; }
        try
        {
            if (File.Exists(UpdateResultPath))
            {
                using var result = System.Text.Json.JsonDocument.Parse(File.ReadAllText(UpdateResultPath));
                _updateStatus = result.RootElement.GetProperty("Message").GetString() ?? "Update finished.";
                _log.Write(result.RootElement.TryGetProperty("Success", out var success) && !success.GetBoolean() ? "error" : "updates", _updateStatus);
                ViewModel.Publish(_updateStatus, true); File.Delete(UpdateResultPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        { _log.Write("updates", "Could not read the last update result", ex); }
        _updateTimer.Tick += async (_, _) => { if (ViewModel.Settings.AutomaticUpdates) await CheckUpdatesAsync(true); };
        _updateTimer.Start();
        if (ViewModel.Settings.AutomaticUpdates) _ = CheckUpdatesAsync(true);
#endif
    }
    private void BuildUpdateSettings()
    {
#if NAGI_DEVELOPMENT
        var development = Stack(); development.Children.Add(Heading("Development updates"));
        development.Children.Add(Note(_updateStatus));
        development.Children.Add(Button("Preview update changelog", async (_, _) => await PreviewDevelopmentUpdateAsync()));
        SettingsPage.Children.Add(Card(development));
#else
        if (Pulse.Windows.WindowsNotificationService.HasIdentity) {
            var packaged = SettingsCardHeader("App updates", "Install the latest signed MSIX to update the packaged edition. Windows preserves its app identity and permission.", "\uE895");
            packaged.Children.Add(Note(L.T("Installed version ") + CurrentVersion.ToString(3)));
            packaged.Children.Add(new HyperlinkButton { Content = L.T("View releases"), NavigateUri = new("https://github.com/alaaeddinemekkaoui/Nagi/releases/latest") });
            SettingsPage.Children.Add(Card(packaged)); return;
        }
        var content = Stack(); content.Children.Add(Heading(L.T("App updates")));
        content.Children.Add(Note(L.T("Installed version ") + CurrentVersion.ToString(3) + L.T(". Updates come from Nagi's GitHub releases. Your profiles, preferences and scripts are preserved.")));
        content.Children.Add(Toggle(L.T("Download updates automatically"), L.T("Check at startup and every six hours, then download verified updates. Choose Install and restart when ready."), ViewModel.Settings.AutomaticUpdates, (s, enabled) => s with { AutomaticUpdates = enabled }));
        _updateStatusText = Note(_updateStatus); content.Children.Add(_updateStatusText);
        _updateProgressView = new Grid { Width = 68, Height = 68, HorizontalAlignment = HorizontalAlignment.Left };
        _updateProgressRing = new Controls.TimerRing(); _updateProgressLabel = new TextBlock { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _updateProgressView.Children.Add(_updateProgressRing); _updateProgressView.Children.Add(_updateProgressLabel); content.Children.Add(_updateProgressView);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_updateProgressView, "UpdateTransferProgress");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_updateStatusText, L.T("Update status"));
        _updateNotesText = Note(ReleaseNotes());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_updateNotesText, L.T("Update changelog"));
        _checkUpdateButton = Button(L.T("Check for updates"), async (_, _) => await CheckUpdatesAsync(false));
        _installUpdateButton = Button(_preparedUpdate is null ? L.T("Download and update") : L.T("Install and restart"), async (_, _) => await InstallUpdateAsync());
        content.Children.Add(_checkUpdateButton); content.Children.Add(_installUpdateButton);
        content.Children.Add(_updateNotesText);
        content.Children.Add(new HyperlinkButton { Content = L.T("View releases"), NavigateUri = new("https://github.com/" + AppUpdateService.Repository + "/releases") });
        SettingsPage.Children.Add(Card(content)); RefreshUpdateUi();
#endif
    }
#if !NAGI_DEVELOPMENT
    private void RefreshUpdateUi()
    {
        if (_updateProgressView is not null) {
            _updateProgressView.Visibility = _updateProgress >= 0 ? Visibility.Visible : Visibility.Collapsed;
            _updateProgressRing!.Update(Math.Clamp(_updateProgress / 100, 0, 1), ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush"));
            _updateProgressLabel!.Text = L.F($"{Math.Max(0, _updateProgress):0}%");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_updateProgressView, L.F($"Update download {_updateProgress:0}%"));
        }
        if (_updateStatusText is not null) _updateStatusText.Text = _updateStatus;
        if (_updateNotesText is not null) _updateNotesText.Text = ReleaseNotes();
        if (_checkUpdateButton is not null) _checkUpdateButton.IsEnabled = !_updateBusy;
        if (_installUpdateButton is not null)
        {
            _installUpdateButton.IsEnabled = !_updateBusy && _availableUpdate is not null;
            _installUpdateButton.Content = _preparedUpdate is null ? L.T("Download and update") : L.T("Install and restart");
        }
    }
    private async Task CheckUpdatesAsync(bool automatic)
    {
        if (_closed || _updateBusy || automatic && _preparedUpdate is not null) return;
        _updateBusy = true; _updateStatus = L.T("Checking for updates…"); RefreshUpdateUi();
        var installNow = false;
        try
        {
            _log.Write("updates", $"Checking releases; installed={CurrentVersion.ToString(3)}, automatic={automatic}");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var release = await _updates.CheckAsync(CurrentVersion, deadline.Token);
            if (_closed) return;
            _updateTimer.Interval = TimeSpan.FromHours(6);
            _availableUpdate = release;
            if (_preparedUpdate?.Release.Version != release?.Version) _preparedUpdate = null;
            _updateStatus = release is null ? L.T("You have the latest release.") : L.F($"Nagi {release.Version.ToString(3)} is available");
            _log.Write("updates", _updateStatus);
            RefreshUpdateUi();
            if (release is not null && (!automatic || _offeredUpdateVersion != release.Version)) installNow = await OfferUpdateAsync(release);
            if (release is not null && !installNow && ViewModel.Settings.AutomaticUpdates && _preparedUpdate is null) await DownloadUpdateAsync();
            if (release is not null) ViewModel.Publish(_preparedUpdate is null ? _updateStatus : L.T("An update is ready. Open Settings → Updates to install and restart."), true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException or UriFormatException or KeyNotFoundException)
        { if (!_closed) { _updateStatus = "Could not check for updates. " + ex.Message; _updateTimer.Interval = TimeSpan.FromMinutes(2); _log.Write("updates", "Release check failed; retrying in two minutes", ex); } }
        finally { _updateBusy = false; if (!_closed) RefreshUpdateUi(); }
        if (installNow && !_closed) await InstallUpdateAsync();
    }
    private string ReleaseNotes() => _availableUpdate is { } release
        ? L.F($"What's new in Nagi {release.Version.ToString(3)}") + "\n\n" + (string.IsNullOrWhiteSpace(release.Notes) ? L.T("Release notes are available on GitHub.") : release.Notes)
        : L.T("The changelog will appear here when a new release is detected.");

    private async Task<bool> OfferUpdateAsync(AppRelease release)
    {
        if (_closed || _dialogOpen) return false;
        _offeredUpdateVersion = release.Version;
        var wasExpanded = _expanded;
        OpenControlCenter("Settings"); ShowSettingsCategory("Updates");
        var content = Stack(); content.Children.Add(Note(L.T("Installed: ") + CurrentVersion.ToString(3)));
        var notes = Note(ReleaseNotes());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(notes, L.T("Available update changelog"));
        content.Children.Add(notes);
        if (release.ReleasePage is { } page) content.Children.Add(new HyperlinkButton { Content = L.T("View release on GitHub"), NavigateUri = page });
        var dialog = Dialog(L.F($"Nagi {release.Version.ToString(3)} is available"), content);
        dialog.PrimaryButtonText = L.T("Update now"); dialog.CloseButtonText = L.T("Later");
        // Avoid installing through an accidental Enter key while the dialog opens.
        dialog.DefaultButton = ContentDialogButton.Close;
        var choice = await ShowDialogAsync(dialog);
        var now = choice == ContentDialogResult.Primary;
        _log.Write("updates", $"Update {release.Version.ToString(3)}: {(now ? "Update now" : "Later")} selected");
        if (!now && !wasExpanded && !_closed) Collapse();
        return now;
    }
    private async Task DownloadUpdateAsync()
    {
        if (_availableUpdate is null) return;
        _log.Write("updates", "Downloading Nagi " + _availableUpdate.Version.ToString(3));
        _updateProgress = 0; _updateStatus = L.T("Downloading update…"); SetUpdateTransfer(0, true); RefreshUpdateUi();
        var progress = new Progress<double>(value => { if (!_closed && _preparedUpdate is null) { _updateProgress = value; SetUpdateTransfer(value, true); _updateStatus = L.F($"Downloading update… {value:0}%"); RefreshUpdateUi(); } });
        var updateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nagi", "Updates");
        try { _preparedUpdate = await _updates.PrepareAsync(_availableUpdate, updateRoot, progress, _shutdown.Token); }
        finally { if (!_closed) SetUpdateTransfer(_updateProgress, false); }
        _updateProgress = 100;
        _updateStatus = L.T("Update verified and ready. Install and restart to finish.");
        _log.Write("updates", "Package verified for Nagi " + _preparedUpdate.Release.Version.ToString(3));
    }
    private async Task InstallUpdateAsync()
    {
        if (_closed || _updateBusy || _availableUpdate is null) return;
        _updateBusy = true; RefreshUpdateUi();
        try
        {
            if (_preparedUpdate is null) await DownloadUpdateAsync();
            if (_closed || _preparedUpdate is null) return;
            var executable = Environment.ProcessPath!;
            var helper = Path.Combine(_preparedUpdate.Directory, "Apply-NagiUpdate.ps1");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Apply-NagiUpdate.ps1"), helper, true);
            var plan = new
            {
                ProcessId = Environment.ProcessId, Executable = executable, Destination = Path.GetDirectoryName(executable),
                Payload = _preparedUpdate.Payload, ResultPath = UpdateResultPath, Version = _preparedUpdate.Release.Version.ToString(3),
                DataDirectory = ((App)Application.Current).LaunchOptions.DataDirectory,
                Files = Directory.GetFiles(_preparedUpdate.Payload, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(_preparedUpdate.Payload, p)).ToArray()
            };
            var planPath = Path.Combine(_preparedUpdate.Directory, "plan.json");
            await File.WriteAllTextAsync(planPath, System.Text.Json.JsonSerializer.Serialize(plan), _shutdown.Token);
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", helper, "-PlanPath", planPath }) start.ArgumentList.Add(argument);
            using var updater = Process.Start(start) ?? throw new InvalidOperationException("The updater could not start.");
            _log.Write("updates", "Starting installation and restart for Nagi " + _preparedUpdate.Release.Version.ToString(3));
            _updateStatus = L.T("Restarting to install the update…"); Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        { if (!_closed) { _updateStatus = "Update could not be installed. " + ex.Message; _log.Write("updates", "Update failed", ex); } }
        finally { _updateBusy = false; if (!_closed) RefreshUpdateUi(); }
    }
#endif
}
