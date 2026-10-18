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
        _log.Write("profiles", profile is { } selected ? "Activating " + selected : "Activating automatic profiles");
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
                ViewModel.Publish(failures == 0 ? active + L.T(" workspace opened.") : active + L.T(" activated. ") + failures + L.T(" app(s) unavailable; update the profile's app list."), true);
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
        var dockApps = ProfileWorkspaces.DockApps(workspace);
        if (_preview == PreviewKind.Apps && (_folderProfile != ViewModel.Profile || !DockPresentation.GroupApps(dockApps.Length))) ClosePreview();
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
        WorkspaceTitle.Text = L.F($"{ViewModel.ProfileText} workspace");
        WorkspaceHint.Text = L.T("Apps and widgets follow your active profile.");
        foreach (var app in workspace.Apps)
        {
            Button AppButton(bool rail, bool folder = false)
            {
                var copy = new Grid { ColumnSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
                copy.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); copy.ColumnDefinitions.Add(new());
                var resolved = ProfileWorkspaces.Resolve(app, _appCatalog.Entries);
                copy.Children.Add(new Controls.AppIcon(app.Name, resolved?.Target ?? (app.Target == "terminal" ? "terminal" : null), ThemeBrush("InkBrush"), rail ? 18 : 20));
                copy.Children.Add(new TextBlock { Text = app.Name, FontSize = rail ? 8 : 12, MaxWidth = rail ? 56 : 170, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                Grid.SetColumn((FrameworkElement)copy.Children[1], 1);
                var button = new Button { Content = copy, CornerRadius = new(10), Padding = new(rail ? 4 : 12), MinHeight = rail ? 36 : 44, HorizontalAlignment = HorizontalAlignment.Stretch };
                if (rail) { button.Style = (Style)Application.Current.Resources["QuietButton"]; button.MinWidth = button.MinHeight = 0; button.Width = 34; button.Height = horizontal ? 36 : 32; copy.Children.RemoveAt(1); copy.ColumnDefinitions.Clear(); copy.ColumnDefinitions.Add(new()); }
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                AutomationProperties.SetName(button, (folder ? L.T("Folder app ") : rail ? L.T("Dock app ") : L.T("Workspace app ")) + app.Name);
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
            if (dockApps.Contains(app) && !DockPresentation.GroupApps(dockApps.Length)) ProfileDockPanel.Children.Add(AppButton(true));
            else if (dockApps.Contains(app))
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
        if (DockPresentation.GroupApps(dockApps.Length))
        {
            AppFolderButton.Width = 34;
            AppFolderButton.Visibility = Visibility.Visible;
            AppFolderCount.Text = dockApps.Length.ToString();
            AppFolderHint.Text = dockApps.Length + L.T(" apps") + " · " + ViewModel.ProfileText;
            ToolTipService.SetToolTip(AppFolderButton, L.T("More apps · hover to expand ") + dockApps.Length + L.T(" apps"));
            ProfileDockPanel.Children.Add(AppFolderButton);
        }
        WorkspaceAppsCard.Visibility = workspace.Apps.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ProfileDockPanel.Visibility = dockApps.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfigureObsDock();
        WorkspaceHint.Text = workspace.Apps.Length == 0 ? L.T("Add apps from Profiles to build this workspace.") : workspace.AutoOpenApps ? L.T("Apps open when you manually activate this profile.") : L.T("Click an app to open it. Automatic opening is off.");
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
        IntegrationCard.Visibility = Show(ProfileWorkspaces.ShowObs(workspace) && workspace.ObsPresentation == "Widget");
        RailObsButton.Visibility = Show(ProfileWorkspaces.ShowObs(workspace));
        WeatherCard.Visibility = RailWeatherButton.Visibility = Show(workspace.Shows("Weather") && ViewModel.Settings.WeatherEnabled);
        CompactCpuButton.Visibility = RailGpuButton.Visibility = Show(workspace.Shows("Performance"));
        CompactMediaButton.Visibility = Show(workspace.Shows("Media") && ViewModel.Settings.ShowMedia || workspace.Shows("Audio"));
        RailMicrophoneButton.Visibility = Show(workspace.Shows("Microphone"));
        CompactBatteryButton.Visibility = Show(DockPresentation.ShowBattery(workspace, ViewModel.Metrics.Power));
    }

    private void BuildWorkspaceEditor(Profile profile)
    {
        var workspace = ViewModel.Settings.Workspace(profile);
        WorkspacePage.Children.Add(Button(L.T("← All profiles"), (_, _) => { _editingWorkspace = null; BuildRulesPage(); }));
        WorkspacePage.Children.Add(Heading(L.F($"{L.T(profile.ToString())} workspace")));
        WorkspacePage.Children.Add(Note(L.T("Choose the dock apps and widgets shown whenever this profile is active.")));
        WorkspacePage.Children.Add(Button(L.T("Activate ") + profile, async (_, _) => await ActivateProfileAsync(profile)));
        WorkspacePage.Children.Add(Toggle(L.T("Open apps when I activate this profile"), L.T("Runs on manual activation from Profiles, the profile picker, shortcuts or tray. Automatic context switching only changes widgets and the dock."), workspace.AutoOpenApps,
            (s, value) => s with { Workspaces = s.Workspaces.Select(w => w.Profile == profile ? w with { AutoOpenApps = value } : w).ToArray() }));
        WorkspacePage.Children.Add(Note(L.T("Automatic context switching updates the dock and widgets without opening apps. Startup does not open profile apps.")));
        var apps = Stack(); apps.Children.Add(Heading(L.T("App dock")));
        apps.Children.Add(Note(L.T("Up to two apps appear directly on the dock. More than two apps share one folder that expands on hover or click. Overview shows all apps, up to four per row.")));
        foreach (var app in workspace.Apps)
        {
            var row = new Grid { ColumnSpacing = 8 }; row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = app.Name + (app.Kind == "Suggested" ? L.T(" · suggested") : ""), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var remove = Button(L.T("Remove ") + app.Name, async (_, _) => { var current = ViewModel.Settings.Workspace(profile); if (await SaveWorkspaceAsync(current with { Apps = current.Apps.Where(a => a != app).ToArray() })) BuildRulesPage(); });
            Grid.SetColumn(remove, 1); row.Children.Add(remove); apps.Children.Add(row);
        }
        var picker = new ComboBox { ItemsSource = _appCatalog.Entries.OrderBy(e => e.Title).ToArray(), DisplayMemberPath = "Title", IsEditable = true, IsTextSearchEnabled = true, PlaceholderText = L.T("Choose an installed app"), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(picker, L.T("Workspace app picker")); apps.Children.Add(picker);
        apps.Children.Add(Button(L.T("Add selected app"), async (_, _) =>
        {
            if (picker.SelectedItem is not SearchEntry app) { ViewModel.Publish(L.T("Select an installed app from the list."), true); return; }
            var current = ViewModel.Settings.Workspace(profile);
            if (current.Apps.Length >= 16) { ViewModel.Publish(L.T("A profile supports up to 16 apps."), true); return; }
            if (await SaveWorkspaceAsync(current with { Apps = [.. current.Apps, new(app.Title, app.Target)] })) BuildRulesPage();
        }));
        apps.Children.Add(Button(L.T("Refresh installed apps"), async (_, _) => { _appCatalog = await SearchApplications.ReadAsync(_shutdown.Token); if (!_closed) BuildRulesPage(); }));
        WorkspacePage.Children.Add(Card(apps));
        var widgets = Stack(); widgets.Children.Add(Heading(L.T("Visible widgets")));
        foreach (var widget in ProfileWorkspaces.WidgetNames)
        {
            var toggle = new CheckBox { Content = L.T(widget), IsChecked = workspace.Shows(widget) };
            AutomationProperties.SetName(toggle, profile + L.T(" widget ") + widget);
            toggle.Click += async (_, _) =>
            {
                var current = ViewModel.Settings.Workspace(profile);
                var updated = toggle.IsChecked == true ? current.Widgets.Append(widget).Distinct().ToArray() : current.Widgets.Where(w => w != widget).ToArray();
                if (!await SaveWorkspaceAsync(current with { Widgets = updated })) toggle.IsChecked = current.Shows(widget);
            };
            widgets.Children.Add(toggle);
        }
        WorkspacePage.Children.Add(Card(widgets));
        AddObsPresentationSettings(WorkspacePage, profile);
    }
}
