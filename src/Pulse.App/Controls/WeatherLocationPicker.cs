using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Infrastructure;

namespace Pulse.App.Controls;

public sealed class WeatherLocationPicker : StackPanel
{
    private readonly TextBox _query = new() { Header = "City", PlaceholderText = "Search a city, optionally followed by country", MaxLength = 100 };
    private readonly Button _search = new() { Content = "Search cities" };
    private readonly ListBox _cities = new() { DisplayMemberPath = nameof(WeatherLocation.Label), MaxHeight = 160, SelectionMode = SelectionMode.Single };
    private readonly ComboBox _units = new() { Header = "Temperature unit", ItemsSource = new[] { "Celsius", "Fahrenheit" }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _status = new() { Text = "City searches go to Open-Meteo. Device location is not accessed.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = .8 };
    public WeatherLocation? SelectedLocation => _cities.SelectedItem as WeatherLocation;
    public string SelectedUnit => _units.SelectedItem as string ?? "Celsius";
    public event Action? ChoiceChanged;
    public WeatherLocationPicker(WeatherService service, WeatherLocation? selected, string unit, CancellationToken lifetime)
    {
        Spacing = 12;
        AutomationProperties.SetAutomationId(_query, "WeatherCityQuery");
        AutomationProperties.SetName(_cities, "Matching cities"); AutomationProperties.SetAutomationId(_cities, "WeatherCities");
        AutomationProperties.SetAutomationId(_search, "SearchWeatherCities");
        AutomationProperties.SetLiveSetting(_status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _units.SelectedItem = unit == "Fahrenheit" ? "Fahrenheit" : "Celsius";
        _cities.ItemsSource = selected is { IsValid: true } ? new[] { selected } : [];
        if (selected is { IsValid: true }) { _cities.SelectedIndex = 0; _query.Text = selected.Name; }
        _cities.SelectionChanged += (_, _) => ChoiceChanged?.Invoke(); _units.SelectionChanged += (_, _) => ChoiceChanged?.Invoke();
        _search.Click += async (_, _) =>
        {
            _search.IsEnabled = false; _cities.ItemsSource = Array.Empty<WeatherLocation>(); ChoiceChanged?.Invoke();
            _status.Text = "Searching cities…";
            try
            {
                var cities = await service.SearchAsync(_query.Text, lifetime);
                if (lifetime.IsCancellationRequested) return;
                _cities.ItemsSource = cities;
                if (cities.Length == 1) _cities.SelectedIndex = 0;
                _status.Text = cities.Length == 0 ? "No matching cities. Try a nearby city or add a country." : "Choose your city from the results.";
            }
            catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) _status.Text = "The search timed out. Try again, or skip weather for now."; }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or InvalidDataException)
            { if (!lifetime.IsCancellationRequested) _status.Text = ex is InvalidDataException ? ex.Message : "Can't reach the weather service. Try again, or skip for now."; }
            finally { if (!lifetime.IsCancellationRequested) _search.IsEnabled = true; }
        };
        Children.Add(_query); Children.Add(_search); Children.Add(_cities); Children.Add(_units); Children.Add(_status);
    }
    public void Validation(string message) => _status.Text = message;
}
