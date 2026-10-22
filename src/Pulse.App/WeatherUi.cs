using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Infrastructure;
using Pulse.App.Controls;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly WeatherService _weather = new();
    private Task? _weatherPolling;
    private WeatherSnapshot _weatherSnapshot = WeatherSnapshot.Disabled;
    private async Task PollWeatherAsync()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var settings = Volatile.Read(ref _pollSettings);
                var weather = await _weather.SampleAsync(settings, token);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_closed || settings.WeatherEnabled != ViewModel.Settings.WeatherEnabled
                        || settings.WeatherLocation != ViewModel.Settings.WeatherLocation || settings.WeatherUnit != ViewModel.Settings.WeatherUnit) return;
                    _weatherSnapshot = weather; RenderWeather();
                });
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        }
    }
    private void RenderWeather()
    {
        var settings = ViewModel.Settings;
        var enabled = settings.WeatherEnabled && settings.WeatherLocation is { IsValid: true };
        WeatherCard.Visibility = RailWeatherButton.Visibility = enabled && settings.Workspace(ViewModel.Profile).Shows("Weather") ? Visibility.Visible : Visibility.Collapsed;
        if (!enabled) { UpdateUtilityWeather(); return; }
        var current = _weatherSnapshot.Location == settings.WeatherLocation && _weatherSnapshot.Unit == settings.WeatherUnit
            ? _weatherSnapshot : new(settings.WeatherLocation, null, null, null, null, true, null, settings.WeatherUnit, "Checking weather…");
        WeatherCityText.Text = settings.WeatherLocation!.Label;
        WeatherHeadline.Text = current.Temperature.HasValue ? current.TemperatureText + " · " + L.T(current.Conditions) : L.T(current.Status);
        WeatherDetailText.Text = (current.Stale ? L.T("Last available · ") : "")
            + (current.FeelsLike is { } feels ? L.F($"Feels like {feels:0}° · ") : "")
            + (current.WindKmh is { } wind ? L.F($"Wind {wind:0} km/h · ") : "")
            + (current.ObservedAt is { } time ? L.F($"As of {time.ToLocalTime():HH:mm}") : L.T(current.Status));
        RailWeatherValue.Text = current.TemperatureText;
        ToolTipService.SetToolTip(RailWeatherButton, settings.WeatherLocation.Label + " · " + WeatherHeadline.Text + (current.Stale ? L.T(" · Last available") : ""));
        UpdateUtilityWeather();
    }
    private void Weather_Click(object sender, RoutedEventArgs args) => OpenControlCenter("Utilities");
    private void BuildPersonalSettings()
    {
        var profile = Stack(); profile.Children.Add(Heading(L.T("Your space")));
        AddLanguageSettings(profile);
        var name = new TextBox { Header = L.T("Your name"), Text = ViewModel.Settings.UserName, MaxLength = 32, PlaceholderText = L.T("Name shown in your greeting") };
        profile.Children.Add(name);
        profile.Children.Add(Button(L.T("Save name"), async (_, _) => await CommitAsync(s => s with { UserName = name.Text })));
        profile.Children.Add(Note(L.T("Your display name stays on this PC.")));
        profile.Children.Add(Button(L.T("Run welcome setup"), (_, _) => RestartWelcome()));
        profile.Children.Add(Note(L.T("Revisit your name, theme, layout, OBS and city. Existing choices stay filled in; canceling keeps your saved settings.")));
        SettingsPage.Children.Add(Card(profile));
    }
    private void BuildWeatherSettings()
    {
        var weather = Stack(); weather.Children.Add(Heading(L.T("Weather")));
        weather.Children.Add(Note(ViewModel.Settings.WeatherLocation?.Label ?? L.T("Choose a city to show weather.")));
        if (ViewModel.Settings.WeatherLocation is { IsValid: true })
            weather.Children.Add(Toggle(L.T("Show weather"), L.T("Current model conditions from Open-Meteo, refreshed about every 15 minutes."), ViewModel.Settings.WeatherEnabled, (s, v) => s with { WeatherEnabled = v }));
        weather.Children.Add(Button(L.T("Choose weather location"), async (_, _) =>
        {
            var picker = new WeatherLocationPicker(_weather, ViewModel.Settings.WeatherLocation, ViewModel.Settings.WeatherUnit, _shutdown.Token);
            var dialog = Dialog(L.T("Weather location"), picker);
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                if (picker.SelectedLocation is not { IsValid: true } city) { picker.Validation(L.T("Search for and select a city first.")); args.Cancel = true; return; }
                var defer = args.GetDeferral();
                try { if (!await CommitAsync(s => s with { WeatherLocation = city, WeatherUnit = picker.SelectedUnit, WeatherEnabled = true })) args.Cancel = true; }
                finally { defer.Complete(); }
            };
            await ShowDialogAsync(dialog); BuildSettingsPage();
        }));
        weather.Children.Add(Note(L.T("Enabling weather sends only the selected city coordinates to Open-Meteo. City search uses GeoNames data. No GPS or IP location lookup.")));
        SettingsPage.Children.Add(Card(weather));
    }
}
