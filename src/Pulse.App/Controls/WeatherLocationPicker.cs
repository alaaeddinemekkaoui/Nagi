using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Infrastructure;

namespace Pulse.App.Controls;

public sealed class WeatherLocationPicker : StackPanel
{
    private readonly TextBox _query = new() { Header = L.T("City"), PlaceholderText = L.T("Search a city, optionally followed by country"), MaxLength = 100 };
    private readonly Button _search = new() { Content = L.T("Search cities") };
    private readonly ListBox _cities = new() { DisplayMemberPath = nameof(WeatherLocation.Label), MaxHeight = 160, SelectionMode = SelectionMode.Single };
    private readonly ComboBox _units = new() { Header = L.T("Temperature unit"), ItemsSource = new[] { "Celsius", "Fahrenheit" }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _status = new() { Text = L.T("City searches go to Open-Meteo. Device location is not accessed."), FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = .8 };
    public WeatherLocation? SelectedLocation => _cities.SelectedItem as WeatherLocation;
    public string SelectedUnit => _units.SelectedItem as string ?? "Celsius";
    public event Action? ChoiceChanged;
    public WeatherLocationPicker(WeatherService service, WeatherLocation? selected, string unit, CancellationToken lifetime)
    {
        Spacing = 12; RefreshLanguage();
        AutomationProperties.SetAutomationId(_query, "WeatherCityQuery");
        AutomationProperties.SetName(_cities, L.T("Matching cities")); AutomationProperties.SetAutomationId(_cities, "WeatherCities");
        AutomationProperties.SetAutomationId(_search, "SearchWeatherCities");
        AutomationProperties.SetLiveSetting(_status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _units.SelectedItem = unit == "Fahrenheit" ? "Fahrenheit" : "Celsius";
        _cities.ItemsSource = selected is { IsValid: true } ? new[] { selected } : [];
        if (selected is { IsValid: true }) { _cities.SelectedIndex = 0; _query.Text = selected.Name; }
        _cities.SelectionChanged += (_, _) => ChoiceChanged?.Invoke(); _units.SelectionChanged += (_, _) => ChoiceChanged?.Invoke();
        _search.Click += async (_, _) =>
        {
            _search.IsEnabled = false; _cities.ItemsSource = Array.Empty<WeatherLocation>(); ChoiceChanged?.Invoke();
            _status.Text = L.T("Searching cities…");
            try
            {
                var cities = await service.SearchAsync(_query.Text, lifetime);
                if (lifetime.IsCancellationRequested) return;
                _cities.ItemsSource = cities;
                if (cities.Length == 1) _cities.SelectedIndex = 0;
                _status.Text = cities.Length == 0 ? L.T("No matching cities. Try a nearby city or add a country.") : L.T("Choose your city from the results.");
            }
            catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) _status.Text = L.T("The search timed out. Try again, or skip weather for now."); }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or InvalidDataException)
            { if (!lifetime.IsCancellationRequested) _status.Text = ex is InvalidDataException ? ex.Message : L.T("Can't reach the weather service. Try again, or skip for now."); }
            finally { if (!lifetime.IsCancellationRequested) _search.IsEnabled = true; }
        };
        Children.Add(_query); Children.Add(_search); Children.Add(_cities); Children.Add(_units); Children.Add(_status);
    }
    public void RefreshLanguage()
    {
        FlowDirection = L.Direction;
        _query.Header = L.T("City"); _query.PlaceholderText = L.T("Search a city, optionally followed by country");
        _search.Content = L.T("Search cities"); _units.Header = L.T("Temperature unit"); L.Options(_units);
        _status.Text = L.T("City searches go to Open-Meteo. Device location is not accessed.");
    }
    public void Validation(string message) => _status.Text = message;
}
