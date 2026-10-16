using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private string? _settingsCategory;
    private void BuildSettingsPage()
    {
        SettingsPage.Children.Clear();
        if (_settingsCategory is null)
        {
            SettingsPage.Children.Add(Heading("Settings"));
            SettingsPage.Children.Add(Note("Choose a category to personalize your space."));
            var categories = new (string Title, string Description, string Glyph)[]
            {
                ("General", "Name and welcome setup", "\uE77B"),
                ("Appearance & dock", "Glass, Akeno background, layout and display", "\uE790"),
                ("Profiles & apps", "Workspaces, app docks and visible widgets", "\uE8A5"),
                ("Weather", "City, units and current conditions", "\uE753"),
                ("Hardware & studio", "GPU, microphone, FPS and OBS", "\uE7F4"),
                ("Search & shortcuts", "App search, file indexing and search key", "\uE721"),
                ("Lua UI studio", "Scripts, presets and custom styling", "\uE943"),
                ("Behavior", "Shortcuts, startup, media and notifications", "\uE713"),
                ("About & diagnostics", "Providers, recent activity and reset", "\uE946")
            };
            var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
            grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
            for (var i = 0; i < categories.Length; i++)
            {
                if (i % 2 == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                var category = categories[i];
                var content = new Grid { ColumnSpacing = 12 }; content.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); content.ColumnDefinitions.Add(new()); content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                content.Children.Add(new FontIcon { Glyph = category.Glyph, FontSize = 21, Foreground = ThemeBrush("PulseAccentBrush"), VerticalAlignment = VerticalAlignment.Center });
                var text = Stack(); text.Spacing = 4; text.Children.Add(new TextBlock { Text = category.Title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                text.Children.Add(new TextBlock { Text = category.Description, FontSize = 11, Foreground = ThemeBrush("MutedBrush"), TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(text, 1); content.Children.Add(text);
                var arrow = new FontIcon { Glyph = "\uE76C", FontSize = 11 }; Grid.SetColumn(arrow, 2); content.Children.Add(arrow);
                var tile = new Button { Content = content, MinHeight = 90, Padding = new(16), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new(16), Background = ThemeBrush("SurfaceBrush"), BorderThickness = new(0) };
                AutomationProperties.SetName(tile, category.Title + " settings");
                tile.Click += (_, _) =>
                {
                    if (category.Title == "Profiles & apps") { _editingWorkspace = null; Navigate("Profiles"); return; }
                    _settingsCategory = category.Title; BuildSettingsPage(); ContentScroll.ChangeView(null, 0, null); Animate(SettingsPage);
                };
                _motion.Wire(tile); Grid.SetRow(tile, i / 2); Grid.SetColumn(tile, i % 2); grid.Children.Add(tile);
            }
            SettingsPage.Children.Add(grid);
        }
        else
        {
            SettingsPage.Children.Add(Button("← All settings", (_, _) => { _settingsCategory = null; BuildSettingsPage(); ContentScroll.ChangeView(null, 0, null); Animate(SettingsPage); }));
            SettingsPage.Children.Add(Heading(_settingsCategory));
            switch (_settingsCategory)
            {
                case "General": BuildPersonalSettings(); break;
                case "Appearance & dock": BuildAppearanceSettings(); break;
                case "Weather": BuildWeatherSettings(); break;
                case "Hardware & studio": BuildIntegrationSettings(); break;
                case "Search & shortcuts": BuildSearchSettings(); break;
                case "Lua UI studio": BuildLuaSettings(); break;
                case "Behavior": BuildBehaviorSettings(); break;
                case "About & diagnostics": BuildDiagnosticsSettings(); break;
            }
        }
        ApplyLuaVisuals();
    }
}
