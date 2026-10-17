using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.Core;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private Profile? _editingWorkspace;
    private Profile? _folderProfile;
    private int _workspaceActivation;
    private readonly HashSet<Profile> _openingWorkspaces = [];
    private void CustomizeActiveWorkspace_Click(object sender, RoutedEventArgs args)
    { _editingWorkspace = ViewModel.Profile; OpenControlCenter("Profiles"); }

    private void OpenControlCenter(string page = "Overview")
    {
        if (_closed || _suppressLogoClick) return;
        DismissDockIntroduction();
        ResetPreview(); _controlCenter = true; _userHidden = false; _hiding = false;
        _visibilityGeneration++; _motion.Reset(Root); _revealFullscreen = true;
        Navigate(page); _windowManager.SetVisible(true); Activate(); UpdateTray();
    }
    private async Task ActivateProfileAsync(Profile? profile)
    {
        var generation = ++_workspaceActivation;
        if (!await CommitAsync(s => profile is null ? s with { LockedProfile = null, AutomaticSwitching = true } : s with { LockedProfile = profile })) return;
        if (profile is not { } active) return;
        var workspace = ViewModel.Settings.Workspace(active);
        if (!ProfileWorkspaces.ShouldOpenApps(active, workspace, true, _openingWorkspaces.Contains(active))) return;
        _openingWorkspaces.Add(active);
        try
        {
            var failures = 0;
            foreach (var app in workspace.Apps)
            {
                if (_closed || generation != _workspaceActivation) break;
                if (!(await OpenProfileAppAsync(app)).Success) failures++;
            }
            if (!_closed && generation == _workspaceActivation)
                ViewModel.Publish(failures == 0 ? active + " workspace opened." : active + " activated. " + failures + " app(s) unavailable; update the profile's app list.", true);
        }
        catch (OperationCanceledException) when (_closed || _shutdown.IsCancellationRequested) { }
        finally { _openingWorkspaces.Remove(active); }
    }
    private async Task<OperationResult> OpenProfileAppAsync(ProfileApp app)
    {
        var resolved = ProfileWorkspaces.Resolve(app, _appCatalog.Entries);
        if (resolved is null)
        {
            var catalog = await SearchApplications.ReadAsync(_shutdown.Token);
            if (_closed) return OperationResult.Fail("Nagi closed.");
            _appCatalog = catalog; resolved = ProfileWorkspaces.Resolve(app, catalog.Entries);
        }
        return resolved is null ? OperationResult.Fail(app.Name + " is not installed or no longer matches. Choose an installed app in Profiles.")
            : await SearchApplications.OpenAsync(resolved, _shutdown.Token);
    }
    private Task<bool> SaveWorkspaceAsync(ProfileWorkspace workspace) => CommitAsync(s => s with
    { Workspaces = s.Workspaces.Select(w => w.Profile == workspace.Profile ? workspace : w).ToArray() });

    private void BuildProfileDock()
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        if (_preview == PreviewKind.Apps && (_folderProfile != ViewModel.Profile || workspace.Apps.Length <= 3)) ClosePreview();
        _folderProfile = ViewModel.Profile;
        AppFolderButton.Visibility = Visibility.Collapsed;
        AppFolderGrid.Children.Clear(); AppFolderGrid.RowDefinitions.Clear();
        ProfileDockPanel.Children.Clear(); WorkspaceAppsPanel.Children.Clear(); WorkspaceAppsPanel.RowDefinitions.Clear(); WorkspaceAppsPanel.ColumnDefinitions.Clear();
        for (var column = 0; column < 4; column++) WorkspaceAppsPanel.ColumnDefinitions.Add(new());
        var horizontal = ViewModel.Settings.Position.Orientation == "Horizontal";
        var folderColumns = horizontal ? 4 : 2;
        AppFolderGrid.ColumnDefinitions.Clear();
        for (var column = 0; column < folderColumns; column++) AppFolderGrid.ColumnDefinitions.Add(new());
        ProfileDockPanel.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        WorkspaceTitle.Text = ViewModel.Profile + " workspace";
        WorkspaceHint.Text = "Apps and widgets follow your active profile.";
        foreach (var app in workspace.Apps)
        {
            Button AppButton(bool rail, bool folder = false)
            {
                var copy = new Grid { ColumnSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
                copy.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); copy.ColumnDefinitions.Add(new());
                copy.Children.Add(new FontIcon { Glyph = app.Target switch { "code" => "\uE943", "terminal" => "\uE756", "browser" => "\uE774", "steam" => "\uE7FC", _ => "\uE8A5" }, FontSize = rail ? 15 : 18 });
                copy.Children.Add(new TextBlock { Text = app.Name, FontSize = rail ? 8 : 12, MaxWidth = rail ? 56 : 170, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                Grid.SetColumn((FrameworkElement)copy.Children[1], 1);
                var button = new Button { Content = copy, CornerRadius = new(10), Padding = new(rail ? 4 : 12), MinHeight = rail ? 36 : 44, HorizontalAlignment = HorizontalAlignment.Stretch };
                if (rail) { button.Width = horizontal ? 44 : 60; copy.Children.RemoveAt(1); copy.ColumnDefinitions.Clear(); copy.ColumnDefinitions.Add(new()); }
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                AutomationProperties.SetName(button, (folder ? "Folder app " : rail ? "Dock app " : "Workspace app ") + app.Name);
                ToolTipService.SetToolTip(button, app.Name + " · " + ViewModel.Profile);
                button.Click += async (_, _) =>
                {
                    try
                    {
                        var result = await OpenProfileAppAsync(app); Report(result);
                        if (result.Success && folder && _preview == PreviewKind.Apps) ClosePreview();
                        if (!result.Success && (rail || folder)) { _editingWorkspace = ViewModel.Profile; OpenControlCenter("Profiles"); }
                    }
                    catch (OperationCanceledException) { }
                };
                _motion.Wire(button); return button;
            }
            if (WorkspaceAppsPanel.Children.Count < 3) ProfileDockPanel.Children.Add(AppButton(true));
            else
            {
                var folderIndex = AppFolderGrid.Children.Count;
                if (folderIndex % folderColumns == 0) AppFolderGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                var folderApp = AppButton(false, true); folderApp.Padding = new(8); folderApp.FontSize = 11;
                Grid.SetRow(folderApp, folderIndex / folderColumns); Grid.SetColumn(folderApp, folderIndex % folderColumns); AppFolderGrid.Children.Add(folderApp);
            }
            var index = WorkspaceAppsPanel.Children.Count;
            if (index % 4 == 0) WorkspaceAppsPanel.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var appButton = AppButton(false); Grid.SetRow(appButton, index / 4); Grid.SetColumn(appButton, index % 4);
            WorkspaceAppsPanel.Children.Add(appButton);
        }
        if (workspace.Apps.Length > 3)
        {
            AppFolderButton.Width = horizontal ? 44 : 60;
            AppFolderButton.Visibility = Visibility.Visible;
            AppFolderCount.Text = (workspace.Apps.Length - 3).ToString();
            AppFolderHint.Text = (workspace.Apps.Length - 3) + " more apps · " + ViewModel.Profile;
            ToolTipService.SetToolTip(AppFolderButton, "More apps · hover to expand " + (workspace.Apps.Length - 3) + " apps");
            ProfileDockPanel.Children.Add(AppFolderButton);
        }
        WorkspaceAppsCard.Visibility = workspace.Apps.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ProfileDockPanel.Visibility = workspace.Apps.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceHint.Text = workspace.Apps.Length == 0 ? "Add apps from Profiles to build this workspace." : workspace.AutoOpenApps ? "Apps open when you manually activate this profile." : "Click an app to open it. Automatic opening is off.";
    }

    private void ApplyWorkspaceVisibility()
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        RamMetric.Visibility = ViewModel.Profile == Profile.Default ? Visibility.Collapsed : Visibility.Visible;
        MetricsGrid.ColumnDefinitions[1].Width = ViewModel.Profile == Profile.Default ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        QuickActionsHeader.Visibility = QuickActionsPanel.Visibility = Show(ViewModel.Profile != Profile.Default);
        MetricsGrid.Visibility = Show(workspace.Shows("Performance") && _luaStyle.ShowMetrics);
        NetworkRow.Visibility = Show(workspace.Shows("Network") && _luaStyle.ShowNetwork);
        MediaCard.Visibility = Show(workspace.Shows("Media") && ViewModel.Settings.ShowMedia && _luaStyle.ShowMedia);
        AudioCard.Visibility = Show(workspace.Shows("Audio") && _luaStyle.ShowAudio);
        MicrophoneCard.Visibility = PeekMicrophoneRow.Visibility = Show(workspace.Shows("Microphone"));
        IntegrationCard.Visibility = RailObsButton.Visibility = Show(workspace.Shows("Studio"));
        WeatherCard.Visibility = RailWeatherButton.Visibility = Show(workspace.Shows("Weather") && ViewModel.Settings.WeatherEnabled);
        CompactCpuButton.Visibility = RailGpuButton.Visibility = Show(workspace.Shows("Performance"));
        CompactMediaButton.Visibility = Show(workspace.Shows("Media") && ViewModel.Settings.ShowMedia || workspace.Shows("Audio"));
        RailMicrophoneButton.Visibility = Show(workspace.Shows("Microphone"));
        CompactBatteryButton.Visibility = Show(workspace.Shows("Battery"));
    }

    private void BuildWorkspaceEditor(Profile profile)
    {
        var workspace = ViewModel.Settings.Workspace(profile);
        WorkspacePage.Children.Add(Button("← All profiles", (_, _) => { _editingWorkspace = null; BuildRulesPage(); }));
        WorkspacePage.Children.Add(Heading(profile + " workspace"));
        WorkspacePage.Children.Add(Note("Choose the dock apps and widgets shown whenever this profile is active."));
        WorkspacePage.Children.Add(Button("Activate " + profile, async (_, _) => await ActivateProfileAsync(profile)));
        WorkspacePage.Children.Add(Toggle("Open apps when I activate this profile", "Runs on manual activation from Profiles, the profile picker, shortcuts or tray. Automatic context switching only changes widgets and the dock.", workspace.AutoOpenApps,
            (s, value) => s with { Workspaces = s.Workspaces.Select(w => w.Profile == profile ? w with { AutoOpenApps = value } : w).ToArray() }));
        WorkspacePage.Children.Add(Note("Automatic context switching updates the dock and widgets without opening apps. Startup does not open profile apps."));
        var apps = Stack(); apps.Children.Add(Heading("App dock"));
        apps.Children.Add(Note("The dock shows up to three apps. Extra apps share a folder that expands on hover or click. Overview shows all apps, up to four per row. Suggested apps use installed Windows apps and can be replaced below."));
        foreach (var app in workspace.Apps)
        {
            var row = new Grid { ColumnSpacing = 8 }; row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = app.Name + (app.Kind == "Suggested" ? " · suggested" : ""), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var remove = Button("Remove " + app.Name, async (_, _) => { var current = ViewModel.Settings.Workspace(profile); if (await SaveWorkspaceAsync(current with { Apps = current.Apps.Where(a => a != app).ToArray() })) BuildRulesPage(); });
            Grid.SetColumn(remove, 1); row.Children.Add(remove); apps.Children.Add(row);
        }
        var picker = new ComboBox { ItemsSource = _appCatalog.Entries.OrderBy(e => e.Title).ToArray(), DisplayMemberPath = "Title", IsEditable = true, IsTextSearchEnabled = true, PlaceholderText = "Choose an installed app", HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(picker, "Workspace app picker"); apps.Children.Add(picker);
        apps.Children.Add(Button("Add selected app", async (_, _) =>
        {
            if (picker.SelectedItem is not SearchEntry app) { ViewModel.Publish("Select an installed app from the list.", true); return; }
            var current = ViewModel.Settings.Workspace(profile);
            if (current.Apps.Length >= 16) { ViewModel.Publish("A profile supports up to 16 apps.", true); return; }
            if (await SaveWorkspaceAsync(current with { Apps = [.. current.Apps, new(app.Title, app.Target)] })) BuildRulesPage();
        }));
        apps.Children.Add(Button("Refresh installed apps", async (_, _) => { _appCatalog = await SearchApplications.ReadAsync(_shutdown.Token); if (!_closed) BuildRulesPage(); }));
        WorkspacePage.Children.Add(Card(apps));
        var widgets = Stack(); widgets.Children.Add(Heading("Visible widgets"));
        foreach (var widget in ProfileWorkspaces.WidgetNames)
        {
            var toggle = new CheckBox { Content = widget, IsChecked = workspace.Shows(widget) };
            AutomationProperties.SetName(toggle, profile + " widget " + widget);
            toggle.Click += async (_, _) =>
            {
                var current = ViewModel.Settings.Workspace(profile);
                var updated = toggle.IsChecked == true ? current.Widgets.Append(widget).Distinct().ToArray() : current.Widgets.Where(w => w != widget).ToArray();
                if (!await SaveWorkspaceAsync(current with { Widgets = updated })) toggle.IsChecked = current.Shows(widget);
            };
            widgets.Children.Add(toggle);
        }
        WorkspacePage.Children.Add(Card(widgets));
    }
}
