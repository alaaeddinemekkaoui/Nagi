using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private void BuildModeConnectionsSettings(string section)
    {
        if (section == "GitHub") {
        var github = SettingsCardHeader("GitHub", "Read notifications and the latest Actions run for your pinned repositories.", "\uE943");
        github.Children.Add(Toggle(L.T("Enable GitHub integration"), L.T("Refreshes every five minutes while Developer mode is visible."), ViewModel.Settings.GitHubEnabled, (s, value) => s with { GitHubEnabled = value }));
        var repositories = new TextBox { Header = L.T("Pinned repositories · one owner/repository per line"), Text = string.Join("\n", ViewModel.Settings.PinnedRepositories), AcceptsReturn = true, MaxLength = 1200, MinHeight = 80, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(repositories, "PinnedRepositories"); github.Children.Add(repositories);
        var credential = new PasswordBox { Header = L.T("GitHub personal access token"), PlaceholderText = _githubCredential.Exists ? L.T("Saved securely · enter to replace") : L.T("Optional for public repository builds"), MaxLength = 512 };
        AutomationProperties.SetAutomationId(credential, "GitHubToken"); github.Children.Add(credential);
        github.Children.Add(Note(L.T("Notifications need a GitHub token with notifications access. Private repositories also need repository access. Nagi reads data and never marks notifications as read or changes a repository.")));
        github.Children.Add(Button(L.T("Save GitHub connection"), async (_, _) =>
        {
            var values = repositories.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length > 6 || values.Any(v => RepositoryName.Normalize(v) is null)) { ViewModel.Publish(L.T("Choose up to six valid owner/repository names or GitHub repository links."), true); return; }
            try
            {
                if (credential.Password.Length > 0) { _githubCredential.Save(credential.Password); Volatile.Write(ref _githubToken, credential.Password); credential.Password = ""; }
                if (await CommitAsync(s => s with { GitHubEnabled = true, PinnedRepositories = values })) { Volatile.Write(ref _githubRefreshAfter, 0); Wake(_modeWake); RebuildModePanels(); BuildSettingsPage(); }
            }
            catch (Exception ex) { _log.Write("modes", "Could not save GitHub credential", ex); ViewModel.Publish(L.T("Could not save the GitHub connection."), true); }
        }));
        github.Children.Add(Button(L.T("Disconnect GitHub"), async (_, _) => { _githubCredential.Clear(); Volatile.Write(ref _githubToken, ""); await CommitAsync(s => s with { GitHubEnabled = false }); _developer = new(null, [], "Connect GitHub in Integrations"); RebuildModePanels(); BuildSettingsPage(); }));
        github.Children.Add(Note(L.T(_developer.Status))); SettingsPage.Children.Add(Card(github));
        return;
        }

        var calendar = SettingsCardHeader("Calendar", "Show your next meeting in Focus mode using an .ics file or HTTPS calendar feed.", "\uE787");
        calendar.Children.Add(Toggle(L.T("Enable calendar integration"), L.T("Refreshes every five minutes while Focus mode is visible."), ViewModel.Settings.CalendarEnabled, (s, value) => s with { CalendarEnabled = value }));
        var source = new PasswordBox { Header = L.T("HTTPS calendar link or absolute .ics file path"), MaxLength = 512, PlaceholderText = _calendarCredential.Exists ? L.T("Saved securely · enter to replace") : L.T("Paste your calendar source") };
        AutomationProperties.SetAutomationId(source, "CalendarSource"); calendar.Children.Add(source);
        calendar.Children.Add(Button(L.T("Choose .ics file"), async (_, _) =>
        {
            try
            {
                var picker = new global::Windows.Storage.Pickers.FileOpenPicker(); picker.FileTypeFilter.Add(".ics");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                var file = await picker.PickSingleFileAsync(); if (file is not null) source.Password = file.Path;
            }
            catch (Exception ex) { _log.Write("calendar", "Calendar file picker unavailable", ex); ViewModel.Publish(L.T("Could not open file picker."), true); }
        }));
        calendar.Children.Add(Note(L.T("Calendar links can contain private access keys. The source is encrypted for your Windows user. No calendar contents are uploaded to Nagi's website.")));
        calendar.Children.Add(Button(L.T("Save calendar connection"), async (_, _) =>
        {
            var value = source.Password.Length > 0 ? source.Password.Trim() : Volatile.Read(ref _calendarSource);
            var https = Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0;
            if (!https && !(Path.IsPathFullyQualified(value) && value.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))) { ViewModel.Publish(L.T("Choose an HTTPS calendar link or an absolute .ics file path."), true); return; }
            try
            {
                _calendarCredential.Save(value); Volatile.Write(ref _calendarSource, value); source.Password = "";
                if (await CommitAsync(s => s with { CalendarEnabled = true })) { Volatile.Write(ref _calendarRefreshAfter, 0); Wake(_modeWake); RebuildModePanels(); BuildSettingsPage(); }
            }
            catch (Exception ex) { _log.Write("calendar", "Could not save calendar source", ex); ViewModel.Publish(L.T("Could not save the calendar connection."), true); }
        }));
        calendar.Children.Add(Button(L.T("Disconnect calendar"), async (_, _) => { _calendarCredential.Clear(); Volatile.Write(ref _calendarSource, ""); await CommitAsync(s => s with { CalendarEnabled = false }); _calendar = new(null, "Connect a calendar in Integrations"); RebuildModePanels(); BuildSettingsPage(); }));
        calendar.Children.Add(Note(L.T(_calendar.Status))); calendar.Children.Add(Note(L.T("Timed events and daily/weekly recurrence are supported, including exclusions and edited occurrences. Unsupported recurrence or timezone entries are reported. All-day entries are not treated as meetings.")));
        SettingsPage.Children.Add(Card(calendar));
    }
    private void BuildProfileModeEditor(ProfileWorkspace workspace)
    {
        var panel = SettingsCardHeader("Integrated mode", "Choose what this profile helps you do. Each mode connects to real providers.", ModeGlyph(ModePresets.For(workspace)));
        var mode = Select(ModePresets.Names, workspace.Mode, "Integrated mode"); AutomationProperties.SetAutomationId(mode, "ProfileModeSelector");
        mode.SelectionChanged += async (_, _) => { if (mode.SelectedItem is string value && await SaveWorkspaceAsync(ViewModel.Settings.Workspace(workspace.Profile) with { Mode = value })) { _modeUiKey = null; BuildRulesPage(); } };
        panel.Children.Add(mode); panel.Children.Add(Note(L.T(ModePresets.Description(ModePresets.For(workspace)))));
        panel.Children.Add(Toggle(L.T("Show FPS in this profile"), L.T("Gaming enables this by default. Other modes can opt in separately."), workspace.WantsFps(),
            (s, enabled) => s with { FpsEnabled = enabled || s.FpsEnabled, Workspaces = s.Workspaces.Select(w => w.Profile == workspace.Profile ? w with { ShowFps = enabled } : w).ToArray() }));
        panel.Children.Add(Toggle(L.T("Show mode cards and dock button"), L.T("Hide this profile's integrated mode without changing its ordinary widgets."), workspace.ModeEnabled,
            (s, enabled) => s with { Workspaces = s.Workspaces.Select(w => w.Profile == workspace.Profile ? w with { ModeEnabled = enabled } : w).ToArray() }));
        if (ModePresets.For(workspace) == "Focus") {
        var task = new TextBox { Header = L.T("Current focus task"), Text = workspace.FocusTask, MaxLength = 160 };
        AutomationProperties.SetAutomationId(task, "ProfileFocusTask"); panel.Children.Add(task);
        panel.Children.Add(Button(L.T("Save focus task"), async (_, _) => { await SaveWorkspaceAsync(ViewModel.Settings.Workspace(workspace.Profile) with { FocusTask = task.Text }); RebuildModePanels(); }));
        }
        panel.Children.Add(Button(L.T("Apply mode layout"), async (_, _) =>
        {
            var selected = ModePresets.For(ViewModel.Settings.Workspace(workspace.Profile));
            if (await CommitAsync(s => s with { Workspaces = s.Workspaces.Select(w => w.Profile == workspace.Profile ? ModePresets.Apply(w, selected) : w).ToArray(),
                AutomaticGameDetection = selected == "Gaming" || s.AutomaticGameDetection, FpsEnabled = selected == "Gaming" || s.FpsEnabled }))
            { RebuildModePanels(); BuildRulesPage(); }
        }));
        panel.Children.Add(Note(L.T("Applying a layout changes visible widgets. Gaming also enables local game detection and FPS; it never elevates Nagi automatically.")));
        panel.Children.Add(Button(L.T("Configure integrations"), (_, _) => OpenUtilitySettings("Integrations")));
        WorkspacePage.Children.Add(Card(panel));
    }
}
