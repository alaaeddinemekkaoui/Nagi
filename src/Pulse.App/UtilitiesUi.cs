using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private Controls.TimerRing? _utilityTimerRing;
    private TextBlock? _utilityTimerText, _utilityTimerProfile, _utilityWeatherCity, _utilityWeatherValue, _utilityWeatherDetail;
    private Button? _utilityTimerPrimary, _utilityTimerReset;
    private FontIcon? _utilityTimerPrimaryIcon;
    private TimeSpan _timerDuration = TimeSpan.FromMinutes(30);

    private void Utilities_Click(object sender, RoutedEventArgs e) => Navigate("Utilities");

    private void OpenUtilitySettings(string category)
    {
        OpenControlCenter("Settings"); ShowSettingsCategory(category);
    }

    private void BuildUtilitiesPage()
    {
        UtilitiesPage.Children.Clear();
        UtilitiesPage.Children.Add(Heading(L.T("Utilities")));
        UtilitiesPage.Children.Add(Note(L.T("A little time. A look outside. Useful things, together.")));
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });

        var timer = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Stretch };
        timer.Children.Add(new TextBlock { Text = L.T("Time for what matters"), CharacterSpacing = 100,
            FontSize = 10, Foreground = ThemeBrush("MutedBrush"), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var face = new Grid { Width = 240, Height = 240, HorizontalAlignment = HorizontalAlignment.Center };
        _utilityTimerRing = new Controls.TimerRing(); face.Children.Add(_utilityTimerRing);
        var center = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _utilityTimerText = new TextBlock { FontSize = 46, FontWeight = Microsoft.UI.Text.FontWeights.Light,
            TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetAutomationId(_utilityTimerText, "UtilityTimerRemaining"); center.Children.Add(_utilityTimerText);
        center.Children.Add(new TextBlock { Text = L.T("One thing at a time"), FontSize = 9, CharacterSpacing = 100,
            Foreground = ThemeBrush("MutedBrush"), TextAlignment = TextAlignment.Center });
        face.Children.Add(center); timer.Children.Add(face);
        _utilityTimerProfile = new TextBlock { FontSize = 11, TextAlignment = TextAlignment.Center, Foreground = ThemeBrush("MutedBrush") };
        timer.Children.Add(_utilityTimerProfile);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center };
        _utilityTimerReset = UtilityIconButton("\uE72C", "Reset timer", TimerCancel_Click); actions.Children.Add(_utilityTimerReset);
        _utilityTimerPrimary = UtilityIconButton("\uE768", "Start timer", TimerPrimary_Click);
        _utilityTimerPrimary.Width = _utilityTimerPrimary.Height = 54; _utilityTimerPrimary.CornerRadius = new(27);
        _utilityTimerPrimary.Background = ThemeBrush("AccentWashBrush");
        _utilityTimerPrimaryIcon = (FontIcon)_utilityTimerPrimary.Content;
        _utilityTimerPrimaryIcon.Foreground = ThemeBrush("PulseAccentBrush");
        AutomationProperties.SetAutomationId(_utilityTimerPrimary, "UtilityTimerPrimary"); actions.Children.Add(_utilityTimerPrimary);
        actions.Children.Add(UtilityIconButton("\uE713", "Timer settings", (_, _) => OpenUtilitySettings("Timer & live activity")));
        timer.Children.Add(actions);
        var timerWidget = new ToggleSwitch { Header = L.T("Show timer widget"), IsOn = ViewModel.Settings.Workspace(ViewModel.Profile).Shows("Timer"),
            OnContent = L.T("On"), OffContent = L.T("Off"), FontSize = 12, Margin = new(0, 12, 0, 0) };
        AutomationProperties.SetAutomationId(timerWidget, "UtilityTimerWidget");
        timerWidget.Toggled += async (_, _) =>
        {
            var workspace = ViewModel.Settings.Workspace(ViewModel.Profile);
            var widgets = timerWidget.IsOn ? workspace.Widgets.Append("Timer").Distinct().ToArray() : workspace.Widgets.Where(w => w != "Timer").ToArray();
            if (!await SaveWorkspaceAsync(workspace with { Widgets = widgets })) timerWidget.IsOn = workspace.Shows("Timer");
        };
        timer.Children.Add(timerWidget);
        timer.Children.Add(Note(L.T("Set the duration and automatic start in Profiles.")));
        timer.Children.Add(Button(L.T("Customize active profile"), CustomizeActiveWorkspace_Click));
        var timerCard = Card(timer); timerCard.Padding = new(24); timerCard.CornerRadius = new(22); grid.Children.Add(timerCard);

        var weather = Stack(); weather.Spacing = 18;
        var weatherHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        weatherHeader.Children.Add(new FontIcon { Glyph = "\uE753", Foreground = ThemeBrush("PulseAccentBrush"), FontSize = 21 });
        weatherHeader.Children.Add(Heading(L.T("Weather"))); weather.Children.Add(weatherHeader);
        _utilityWeatherCity = new TextBlock { FontSize = 13, Foreground = ThemeBrush("MutedBrush"), TextWrapping = TextWrapping.Wrap };
        _utilityWeatherValue = new TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Light, TextWrapping = TextWrapping.Wrap };
        _utilityWeatherDetail = Note("");
        weather.Children.Add(_utilityWeatherCity); weather.Children.Add(_utilityWeatherValue); weather.Children.Add(_utilityWeatherDetail);
        weather.Children.Add(Button(L.T("Choose weather location"), (_, _) => OpenUtilitySettings("Weather")));
        weather.Children.Add(Note(L.T("Weather is optional. Only your selected city coordinates are sent to Open-Meteo.")));
        weather.Children.Add(new HyperlinkButton { Content = L.T("Weather by Open-Meteo · locations by GeoNames"),
            NavigateUri = new Uri("https://open-meteo.com/"), FontSize = 10, Padding = new(0) });
        var weatherCard = Card(weather); weatherCard.Padding = new(24); weatherCard.CornerRadius = new(22); weatherCard.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(weatherCard, 1); grid.Children.Add(weatherCard);
        grid.SizeChanged += (_, args) =>
        {
            var narrow = args.NewSize.Width < 640;
            Grid.SetColumn(weatherCard, narrow ? 0 : 1); Grid.SetRow(weatherCard, narrow ? 1 : 0);
            Grid.SetColumnSpan(timerCard, narrow ? 2 : 1); Grid.SetColumnSpan(weatherCard, narrow ? 2 : 1);
        };
        UtilitiesPage.Children.Add(grid);
        var shortcuts = Stack(); shortcuts.Children.Add(Heading(L.T("Within reach")));
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        links.Children.Add(Button(L.T("Quick search"), Search_Click));
        links.Children.Add(Button(L.T("Music"), (_, _) => OpenUtilitySettings("Music")));
        shortcuts.Children.Add(links); shortcuts.Children.Add(Note(L.T("Search also handles calculations, apps and indexed filenames.")));
        UtilitiesPage.Children.Add(Card(shortcuts));
        UpdateUtilityTimer(); UpdateUtilityWeather();
    }

    private Button UtilityIconButton(string glyph, string label, RoutedEventHandler action)
    {
        var button = new Button { Width = 38, Height = 38, MinWidth = 0, MinHeight = 0, Padding = new(0), CornerRadius = new(19),
            Style = (Style)Application.Current.Resources["QuietButton"], Content = new FontIcon { Glyph = glyph, FontSize = 19 } };
        AutomationProperties.SetName(button, L.T(label)); ToolTipService.SetToolTip(button, L.T(label));
        button.Click += action; _motion.Wire(button); return button;
    }

    private void TimerPrimary_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.Active) TimerPause_Click(sender, e); else StartTimer();
    }

    private void UpdateUtilityTimer()
    {
        var duration = _timer.Active ? _timerDuration : TimeSpan.FromMinutes(ViewModel.Settings.TimerFor(ViewModel.Profile).Minutes);
        var fraction = _timer.Active ? _timer.Remaining.TotalSeconds / Math.Max(1, duration.TotalSeconds)
            : DateTimeOffset.UtcNow < _completedUntil ? 0 : 1;
        PeekTimerRing.Update(fraction, ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush"));
        if (_utilityTimerText is null) return;
        _utilityTimerText.Text = TimerDisplayText; _utilityTimerText.FontSize = TimerDisplayText.Length > 5 ? 34 : 46;
        _utilityTimerProfile!.Text = ProfileLabel(ViewModel.Profile);
        _utilityTimerRing!.Update(fraction, ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush"));
        _utilityTimerPrimaryIcon!.Glyph = _timer.Running ? "\uE769" : "\uE768";
        var label = L.T(_timer.Running ? "Pause" : _timer.Paused ? "Resume" : "Start timer");
        AutomationProperties.SetName(_utilityTimerPrimary!, label); ToolTipService.SetToolTip(_utilityTimerPrimary!, label);
        _utilityTimerReset!.IsEnabled = _timer.Active || DateTimeOffset.UtcNow < _completedUntil;
    }

    private void UpdateUtilityWeather()
    {
        if (_utilityWeatherCity is null) return;
        var enabled = ViewModel.Settings.WeatherEnabled && ViewModel.Settings.WeatherLocation is { IsValid: true };
        _utilityWeatherCity.Text = enabled ? ViewModel.Settings.WeatherLocation!.Label : L.T("Choose a city to show weather.");
        _utilityWeatherValue!.Text = enabled ? WeatherHeadline.Text : "—";
        _utilityWeatherDetail!.Text = enabled ? WeatherDetailText.Text : L.T("Weather off");
    }
}
