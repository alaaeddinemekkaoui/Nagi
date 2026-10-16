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
        ProfileDockPanel.Children.Clear(); WorkspaceAppsPanel.Children.Clear();
        var horizontal = ViewModel.Settings.Position.Orientation == "Horizontal";
        ProfileDockPanel.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        WorkspaceTitle.Text = ViewModel.Profile + " workspace";
        WorkspaceHint.Text = "Apps and widgets follow your active profile.";
        foreach (var app in workspace.Apps)
        {
            Button AppButton(bool rail)
            {
                var copy = new StackPanel { Spacing = 3, Orientation = rail && !horizontal ? Orientation.Vertical : Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                copy.Children.Add(new FontIcon { Glyph = app.Target switch { "code" => "\uE943", "terminal" => "\uE756", "browser" => "\uE774", "steam" => "\uE7FC", _ => "\uE8A5" }, FontSize = rail ? 15 : 18 });
                copy.Children.Add(new TextBlock { Text = app.Name, FontSize = rail ? 8 : 12, MaxWidth = rail ? 56 : 170, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                var button = new Button { Content = copy, CornerRadius = new(10), Padding = new(rail ? 4 : 12), MinHeight = rail ? 36 : 44, HorizontalAlignment = HorizontalAlignment.Stretch };
                if (rail) { button.Width = horizontal ? 44 : 60; if (horizontal) copy.Children.RemoveAt(1); }
                AutomationProperties.SetName(button, (rail ? "Dock app " : "Workspace app ") + app.Name);
                ToolTipService.SetToolTip(button, app.Name + " · " + ViewModel.Profile);
                button.Click += async (_, _) =>
                {
                    try
                    {
                        var result = await OpenProfileAppAsync(app); Report(result);
                        if (!result.Success && rail) { _editingWorkspace = ViewModel.Profile; OpenControlCenter("Profiles"); }
                    }
                    catch (OperationCanceledException) { }
                };
                _motion.Wire(button); return button;
            }
            if (ProfileDockPanel.Children.Count < 4) ProfileDockPanel.Children.Add(AppButton(true));
            WorkspaceAppsPanel.Children.Add(AppButton(false));
        }
        ProfileDockPanel.Visibility = workspace.Apps.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceHint.Text = workspace.Apps.Length == 0 ? "Add apps from Profiles to build this workspace." : workspace.AutoOpenApps ? "Apps open when you manually activate this profile." : "Click an app to open it. Automatic opening is off.";
    }

    private void ApplyWorkspaceVisibility()
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        MetricsGrid.Visibility = Show(workspace.Shows("Performance") && _luaStyle.ShowMetrics);
        NetworkRow.Visibility = Show(workspace.Shows("Network") && _luaStyle.ShowNetwork);
        MediaCard.Visibility = Show(workspace.Shows("Media") && ViewModel.Settings.ShowMedia && _luaStyle.ShowMedia);
        AudioCard.Visibility = Show(workspace.Shows("Audio") && _luaStyle.ShowAudio);
        MicrophoneCard.Visibility = PeekMicrophoneRow.Visibility = Show(workspace.Shows("Microphone"));
        IntegrationCard.Visibility = Show(workspace.Shows("Studio"));
        WeatherCard.Visibility = RailWeatherButton.Visibility = Show(workspace.Shows("Weather") && ViewModel.Settings.WeatherEnabled);
        CompactCpuButton.Visibility = RailGpuButton.Visibility = Show(workspace.Shows("Performance"));
        CompactMediaButton.Visibility = Show(workspace.Shows("Media") || workspace.Shows("Audio"));
        RailMicrophoneButton.Visibility = Show(workspace.Shows("Microphone"));
        CompactBatteryButton.Visibility = Show(workspace.Shows("Battery"));
    }

    private void BuildWorkspaceEditor(Profile profile)
    {
        var workspace = ViewModel.Settings.Workspace(profile);
        RulesPage.Children.Add(Button("← All profiles", (_, _) => { _editingWorkspace = null; BuildRulesPage(); }));
        RulesPage.Children.Add(Heading(profile + " workspace"));
        RulesPage.Children.Add(Note("Choose the dock apps and widgets shown whenever this profile is active."));
        RulesPage.Children.Add(Button("Activate " + profile, async (_, _) => await ActivateProfileAsync(profile)));
        RulesPage.Children.Add(Toggle("Open apps when I activate this profile", "Runs on manual activation from Profiles, the profile picker, shortcuts or tray. Automatic context switching only changes widgets and the dock.", workspace.AutoOpenApps,
            (s, value) => s with { Workspaces = s.Workspaces.Select(w => w.Profile == profile ? w with { AutoOpenApps = value } : w).ToArray() }));
        RulesPage.Children.Add(Note("Automatic context switching updates the dock and widgets without opening apps. Startup does not open profile apps."));
        var apps = Stack(); apps.Children.Add(Heading("App dock"));
        apps.Children.Add(Note("The edge dock shows the first four apps; all apps appear in Overview. Suggested apps use installed Windows apps and can be replaced below."));
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
        RulesPage.Children.Add(Card(apps));
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
        RulesPage.Children.Add(Card(widgets));
    }
}
