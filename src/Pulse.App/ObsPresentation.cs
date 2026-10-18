using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private void ConfigureObsDock()
    {
        var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
        var shortcut = workspace.ObsPresentation == "App";
        if (shortcut)
        {
            var app = ProfileWorkspaces.Resolve(new("OBS Studio", "obs", "Suggested"), _appCatalog.Entries);
            RailObsButton.Content = new Controls.AppIcon("OBS Studio", app?.Target, ThemeBrush("InkBrush"), 20);
        }
        else RailObsButton.Content = new FontIcon { Glyph = "\uE714", FontSize = 18 };
        AutomationProperties.SetName(RailObsButton, L.T(shortcut ? "Open OBS Studio" : "OBS Studio controls"));
        ToolTipService.SetToolTip(RailObsButton, L.T(shortcut ? "Open OBS Studio" : "Hover for OBS controls"));
        var menu = new MenuFlyout();
        menu.Opening += (_, _) => _quickMenuOpen = true;
        menu.Closed += (_, _) => { _quickMenuOpen = false; if (!_closed) Layout(); };
        var open = new MenuFlyoutItem { Text = L.T("Open OBS Studio"), Icon = new FontIcon { Glyph = "\uE8A7" } };
        open.Click += ObsOpen_Click; menu.Items.Add(open);
        foreach (var mode in new[] { "Widget", "App", "Hidden" })
        {
            var item = new MenuFlyoutItem { Text = L.T(ObsPresentationLabel(mode)), Icon = new FontIcon { Glyph = mode == "Hidden" ? "\uED1A" : "\uE713" } };
            item.Click += async (_, _) => await SetObsPresentationAsync(ViewModel.Profile, mode);
            menu.Items.Add(item);
        }
        RailObsButton.ContextFlyout = menu;
    }

    private static string ObsPresentationLabel(string mode) => mode switch
    { "App" => "App shortcut", "Hidden" => "Hidden", _ => "Hover controls" };

    private void AddObsPresentationSettings(StackPanel parent, Profile profile)
    {
        var workspace = ViewModel.Settings.Workspace(profile);
        var modes = new[] { "Widget", "App", "Hidden" };
        var box = Select(modes.Select(ObsPresentationLabel), ObsPresentationLabel(workspace.ObsPresentation), "OBS dock presentation");
        AutomationProperties.SetAutomationId(box, "ObsDockPresentation");
        box.SelectionChanged += async (_, _) =>
        {
            if (box.SelectedIndex >= 0) await SetObsPresentationAsync(profile, modes[box.SelectedIndex]);
        };
        parent.Children.Add(Labeled(L.T("OBS dock presentation") + " · " + L.T(profile.ToString()), box));
        parent.Children.Add(Note(L.T("Choose an app shortcut, hover controls, or hide OBS for this profile.")));
    }

    private async Task SetObsPresentationAsync(Profile profile, string mode)
    {
        ClosePreview();
        if (!await CommitAsync(settings => settings with
        {
            Workspaces = settings.Workspaces.Select(w => w.Profile == profile ? w with
            { ObsPresentation = mode, Widgets = mode == "Hidden" ? w.Widgets : w.Widgets.Append("Studio").Distinct().ToArray() } : w).ToArray()
        })) return;
        Layout(true);
    }

    private async void ObsShortcut_Click(object sender, RoutedEventArgs args) => await SetObsPresentationAsync(ViewModel.Profile, "App");
    private async void ObsHide_Click(object sender, RoutedEventArgs args) => await SetObsPresentationAsync(ViewModel.Profile, "Hidden");
}
