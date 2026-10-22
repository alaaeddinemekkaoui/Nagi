using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private double? CurrentFps => ViewModel.Settings.FpsEnabled && _fpsSnapshot.ProcessId == ViewModel.Foreground.ProcessId ? _fpsSnapshot.Fps : null;

    private void BuildGamingIntegrationSettings()
    {
        var games = SettingsCardHeader("Gaming", "Switch to your Gaming profile when a supported game becomes active.", "\uE7FC");
        var automatic = Toggle(L.T("Detect games automatically"), L.T("Recognizes Steam, Epic Games and GOG libraries locally. Explicit executable rules and locked profiles take priority."),
            ViewModel.Settings.AutomaticGameDetection, (s, enabled) => s with { AutomaticGameDetection = enabled });
        AutomationProperties.SetAutomationId(automatic, "AutomaticGameDetection"); games.Children.Add(automatic);
        games.Children.Add(Toggle(L.T("Show Nagi over fullscreen apps"), L.T("Keep the dock available over a game. Turn this off to hide Nagi during fullscreen playback."), !ViewModel.Settings.HideInFullscreen, (s, enabled) => s with { HideInFullscreen = !enabled }));
        games.Children.Add(Note(L.T("Uses the active executable, not fullscreen mode or a video's frame rate. Other launchers can use an exact executable rule in Profiles.")));
        var detected = Note(""); games.Children.Add(detected);
        void UpdateGameStatus() => detected.Text = ViewModel.Settings.LockedProfile.HasValue ? L.T("A profile is locked. Automatic game switching will resume when you choose Automatic.")
            : !ViewModel.Settings.AutomaticSwitching ? L.T("Automatic profile switching is off. Choose Automatic in Profiles to use game detection.")
            : ViewModel.Foreground.IsGame ? ViewModel.Foreground.Name + " · " + ViewModel.Foreground.GameSource : L.T("Waiting for a supported game to become active.");
        UpdateGameStatus(); _settingsCardUpdates.Add(UpdateGameStatus);
        games.Children.Add(Button(L.T("Rescan game libraries"), (_, _) => { _games.Invalidate(); ViewModel.Publish(L.T("Game libraries will refresh on the next application check."), true); }));
        var current = Button(L.T("Add a Gaming executable rule"), async (_, _) => await EditRuleAsync(null, Profile.Gaming));
        AutomationProperties.SetAutomationId(current, "AddGamingRule"); games.Children.Add(current);
        games.Children.Add(Button(L.T("Edit Gaming profile"), (_, _) => { _settingsCategory = "Profiles & apps"; BuildSettingsPage(); OpenWorkspaceEditor(Profile.Gaming); }));
        SettingsPage.Children.Add(Card(games));

        var frames = SettingsCardHeader("PresentMon FPS", "Real application frame timing, displayed beside CPU and GPU.", "\uE9D9");
        var capture = Toggle(L.T("Capture foreground app FPS"), L.T("Uses bundled PresentMon 2.6.0. Some Windows configurations require tracing permission. No keyboard/mouse input is tracked."),
            ViewModel.Settings.FpsEnabled, (s, enabled) => s with { FpsEnabled = enabled });
        AutomationProperties.SetAutomationId(capture, "PresentMonCapture"); frames.Children.Add(capture);
        var fps = new TextBlock { FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        AutomationProperties.SetAutomationId(fps, "PresentMonFpsValue"); frames.Children.Add(fps);
        var status = Note(""); frames.Children.Add(status);
        void UpdateFps() { fps.Text = CurrentFps is { } value ? L.F($"{value:0} FPS") : "— FPS"; status.Text = !ViewModel.Settings.FpsEnabled ? L.T("FPS capture off") : _fpsSnapshot.Status; }
        UpdateFps(); _settingsCardUpdates.Add(UpdateFps);
        frames.Children.Add(Note(L.T("FPS measures application presents, not monitor refresh rate. Enable the Performance widget in a profile to show it on the mini bar.")));
        var help = Stack();
        help.Children.Add(Note(L.T("If Windows denies tracing access, run Start-NagiFpsCapture.ps1 for the game's process ID from an Administrator PowerShell. Nagi itself can stay unprivileged.")));
        help.Children.Add(Note(L.T("Game recognition and FPS capture are independent. A high frame rate alone does not identify a game.")));
        var helper = Button(L.T("Start FPS helper…"), async (_, _) =>
        {
            var processId = ViewModel.Foreground.ProcessId;
            var body = Note(L.T("Windows will ask permission to run only the FPS helper as administrator. It traces the selected application's frame timing. It stops when you disable FPS, close Nagi or close that app."));
            var dialog = Dialog(L.T("Allow FPS tracing?"), body); dialog.PrimaryButtonText = L.T("Start helper");
            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary || _closed) return;
            if (!await CommitAsync(s => s with { FpsEnabled = true })) return;
            var result = await Task.Run(() => Pulse.Windows.FpsCaptureHelper.Start(Path.Combine(AppContext.BaseDirectory, "Start-NagiFpsCapture.ps1"), _settings.FilePath, processId, Environment.ProcessId));
            Report(result);
            if (!_closed) BuildSettingsPage();
        });
        AutomationProperties.SetAutomationId(helper, "StartFpsHelper");
        helper.IsEnabled = ViewModel.Foreground.ProcessId > 0 && ViewModel.Foreground.ProcessId != Environment.ProcessId;
        help.Children.Add(helper);
        frames.Children.Add(new Expander { Header = L.T("FPS help"), Content = help, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch, HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch });
        frames.Children.Add(new HyperlinkButton { Content = "PresentMon · Intel / GameTechDev", NavigateUri = new("https://github.com/GameTechDev/PresentMon"), Padding = new(0) });
        SettingsPage.Children.Add(Card(frames));
    }
}
