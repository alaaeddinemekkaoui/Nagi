using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Infrastructure;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private static readonly HttpClient UpdateClient = new() { Timeout = TimeSpan.FromMinutes(15) };
    private readonly AppUpdateService _updates = new(UpdateClient);
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private AppRelease? _availableUpdate;
    private PreparedUpdate? _preparedUpdate;
    private bool _updateBusy;
    private string _updateStatus = "Check for a new release.";
    private TextBlock? _updateStatusText;
    private Button? _checkUpdateButton, _installUpdateButton;
    private static Version CurrentVersion => typeof(MainWindow).Assembly.GetName().Version ?? new(1, 0, 0, 0);
    private string UpdateResultPath => Path.Combine(Path.GetDirectoryName(_settings.FilePath)!, "update-result.json");
    private void InitializeUpdates()
    {
        try
        {
            if (File.Exists(UpdateResultPath))
            {
                using var result = System.Text.Json.JsonDocument.Parse(File.ReadAllText(UpdateResultPath));
                _updateStatus = result.RootElement.GetProperty("Message").GetString() ?? "Update finished.";
                ViewModel.Publish(_updateStatus, true); File.Delete(UpdateResultPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        { _log.Write("updates", "Could not read the last update result", ex); }
        _updateTimer.Tick += async (_, _) => { if (ViewModel.Settings.AutomaticUpdates) await CheckUpdatesAsync(true); };
        _updateTimer.Start();
        if (ViewModel.Settings.AutomaticUpdates) _ = CheckUpdatesAsync(true);
    }
    private void BuildUpdateSettings()
    {
        var content = Stack(); content.Children.Add(Heading("App updates"));
        content.Children.Add(Note("Installed version " + CurrentVersion.ToString(3) + ". Updates come from Nagi's GitHub releases. Your profiles, preferences and scripts are preserved."));
        content.Children.Add(Toggle("Download updates automatically", "Check at startup and every six hours, then download verified updates. Choose Install and restart when ready.", ViewModel.Settings.AutomaticUpdates, (s, enabled) => s with { AutomaticUpdates = enabled }));
        _updateStatusText = Note(_updateStatus); content.Children.Add(_updateStatusText);
        _checkUpdateButton = Button("Check for updates", async (_, _) => await CheckUpdatesAsync(false));
        _installUpdateButton = Button(_preparedUpdate is null ? "Download and update" : "Install and restart", async (_, _) => await InstallUpdateAsync());
        content.Children.Add(_checkUpdateButton); content.Children.Add(_installUpdateButton);
        content.Children.Add(new HyperlinkButton { Content = "View releases", NavigateUri = new("https://github.com/" + AppUpdateService.Repository + "/releases") });
        SettingsPage.Children.Add(Card(content)); RefreshUpdateUi();
    }
    private void RefreshUpdateUi()
    {
        if (_updateStatusText is not null) _updateStatusText.Text = _updateStatus;
        if (_checkUpdateButton is not null) _checkUpdateButton.IsEnabled = !_updateBusy;
        if (_installUpdateButton is not null)
        {
            _installUpdateButton.IsEnabled = !_updateBusy && _availableUpdate is not null;
            _installUpdateButton.Content = _preparedUpdate is null ? "Download and update" : "Install and restart";
        }
    }
    private async Task CheckUpdatesAsync(bool automatic)
    {
        if (_closed || _updateBusy || automatic && _preparedUpdate is not null) return;
        _updateBusy = true; _updateStatus = "Checking for updates…"; RefreshUpdateUi();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var release = await _updates.CheckAsync(CurrentVersion, deadline.Token);
            if (_closed) return;
            _availableUpdate = release;
            if (_preparedUpdate?.Release.Version != release?.Version) _preparedUpdate = null;
            _updateStatus = release is null ? "You have the latest release." : "Nagi " + release.Version.ToString(3) + " is available.";
            if (release is not null && ViewModel.Settings.AutomaticUpdates && _preparedUpdate is null) await DownloadUpdateAsync();
            if (release is not null) ViewModel.Publish(_preparedUpdate is null ? _updateStatus : "An update is ready. Open Settings → Updates to install and restart.", true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException or UriFormatException or KeyNotFoundException)
        { if (!_closed) { _updateStatus = "Could not check for updates. " + ex.Message; _log.Write("updates", "Release check failed", ex); } }
        finally { _updateBusy = false; if (!_closed) RefreshUpdateUi(); }
    }
    private async Task DownloadUpdateAsync()
    {
        if (_availableUpdate is null) return;
        _updateStatus = "Downloading update…"; RefreshUpdateUi();
        var progress = new Progress<double>(value => { if (!_closed && _preparedUpdate is null) { _updateStatus = $"Downloading update… {value:0}%"; RefreshUpdateUi(); } });
        var updateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nagi", "Updates");
        _preparedUpdate = await _updates.PrepareAsync(_availableUpdate, updateRoot, progress, _shutdown.Token);
        _updateStatus = "Update verified and ready. Install and restart to finish.";
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
            _updateStatus = "Restarting to install the update…"; Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        { if (!_closed) { _updateStatus = "Update could not be installed. " + ex.Message; _log.Write("updates", "Update failed", ex); } }
        finally { _updateBusy = false; if (!_closed) RefreshUpdateUi(); }
    }
}
