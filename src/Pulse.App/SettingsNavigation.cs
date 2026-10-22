using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private string? _settingsCategory;
    private readonly StackPanel _settingsProfilesContent = new() { Spacing = 14 };
    private StackPanel WorkspacePage => _page == "Settings" && _settingsCategory == "Profiles & apps" ? _settingsProfilesContent : RulesPage;

    private void ShowSettingsCategory(string? category)
    {
        _settingsCategory = category;
        _editingWorkspace = null;
        BuildSettingsPage();
        ContentScroll.ChangeView(null, 0, null, true);
        Animate(SettingsPage);
    }

    private void SettingsBack_Click(object sender, RoutedEventArgs e) => ShowSettingsCategory(null);

    private void BuildSettingsPage()
    {
        _settingsCardUpdates.Clear();
        _visualizerSettingsPreview = null;
        SettingsPage.Children.Clear();
        SettingsNavigationHeader.Visibility = _page == "Settings" && _settingsCategory is not null ? Visibility.Visible : Visibility.Collapsed;
        SettingsNavigationHeader.FlowDirection = L.Direction;
        SettingsCategoryTitle.Text = L.T(_settingsCategory ?? "Settings");
        ToolTipService.SetToolTip(SettingsCategoryTitle, SettingsCategoryTitle.Text);
        if (_settingsCategory is null)
        {
            SettingsPage.Children.Add(Heading(L.T("Settings")));
            SettingsPage.Children.Add(Note(L.T("Choose a category to personalize your space.")));
            var categories = new (string Title, string Description, string Glyph)[]
            {
                ("General", "Name and welcome setup", "\uE77B"),
                ("Language", L.F($"Current language: {L.Catalog.Selected.NativeName}"), "\uE774"),
                ("Appearance & dock", "Glass, Akeno background, layout and display", "\uE790"),
                ("Profiles & apps", "Workspaces, app docks and visible widgets", "\uE8A5"),
                ("Weather", "City, units and current conditions", "\uE753"),
                ("Hardware & audio", "System health, speakers and microphone", "\uE7F4"),
                ("Integrations", "Gaming, GitHub, Calendar and OBS Studio", "\uE774"),
                ("Search & shortcuts", "App search, file indexing and search key", "\uE721"),
                ("Lua UI studio", "Scripts, presets and custom styling", "\uE943"),
                ("Music", "Preferred music service and now playing", "\uE8D6"),
                ("Timer & live activity", "Countdown and resting media display", "\uE916"),
                ("Updates", "Check, download and install new releases", "\uE895"),
                ("Behavior", "Shortcuts, startup, media and notifications", "\uE713"),
                ("About & diagnostics", "Providers, recent activity and reset", "\uE946")
            };
#if NAGI_DEVELOPMENT
            categories = [.. categories, ("Developer tools", "Preview, inspect and test this development build", "\uEBE8")];
#endif
            var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
            grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
            for (var i = 0; i < categories.Length; i++)
            {
                if (i % 2 == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                var category = categories[i];
                var content = new Grid { ColumnSpacing = 12 }; content.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); content.ColumnDefinitions.Add(new()); content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                content.Children.Add(new FontIcon { Glyph = category.Glyph, FontSize = 21, Foreground = ThemeBrush("PulseAccentBrush"), VerticalAlignment = VerticalAlignment.Center });
                var text = Stack(); text.Spacing = 4; text.Children.Add(new TextBlock { Text = L.T(category.Title), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                text.Children.Add(new TextBlock { Text = L.T(category.Description), FontSize = 11, Foreground = ThemeBrush("MutedBrush"), TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(text, 1); content.Children.Add(text);
                var arrow = new FontIcon { Glyph = "\uE76C", FontSize = 11 }; Grid.SetColumn(arrow, 2); content.Children.Add(arrow);
                var tile = new Button { Content = content, MinHeight = 90, Padding = new(16), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new(16), Background = ThemeBrush("SurfaceBrush"), BorderThickness = new(0) };
                AutomationProperties.SetName(tile, L.F($"{L.T(category.Title)} settings"));
                AutomationProperties.SetAutomationId(tile, "SettingsCategory" + string.Concat(category.Title.Where(char.IsLetterOrDigit)));
                tile.Click += (_, _) =>
                {
                    ShowSettingsCategory(category.Title);
                };
                _motion.Wire(tile); Grid.SetRow(tile, i / 2); Grid.SetColumn(tile, i % 2); grid.Children.Add(tile);
            }
            SettingsPage.Children.Add(grid);
        }
        else
        {
            switch (_settingsCategory)
            {
                case "General": BuildPersonalSettings(); break;
                case "Language":
                    var language = Stack(); AddLanguageSettings(language); SettingsPage.Children.Add(Card(language)); break;
                case "Appearance & dock": BuildAppearanceSettings(); break;
                case "Profiles & apps": SettingsPage.Children.Add(_settingsProfilesContent); BuildRulesPage(); break;
                case "Weather": BuildWeatherSettings(); break;
                case "Hardware & audio": BuildHardwareAudioSettings(); break;
                case "Hardware & studio": // Compatibility with older development links.
                case "Integrations": BuildIntegrationSettings(); break;
                case "Search & shortcuts": BuildSearchSettings(); break;
                case "Lua UI studio": BuildLuaSettings(); break;
                case "Music": BuildMusicSettings(); break;
                case "Timer & live activity": BuildTimerSettings(); break;
                case "Updates": BuildUpdateSettings(); break;
                case "Behavior": BuildBehaviorSettings(); break;
                case "About & diagnostics": BuildDiagnosticsSettings(); break;
#if NAGI_DEVELOPMENT
                case "Developer tools": BuildDevelopmentSettings(); break;
#endif
            }
        }
        ApplyLuaVisuals();
        UpdateRuntimeDemand();
    }
}
