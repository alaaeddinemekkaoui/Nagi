using System.Globalization;
using System.Text.Json;
using Pulse.Core;

namespace Pulse.Infrastructure;

/// <summary>Explicit city lookup and model-based current conditions. No IP/GPS lookup or API keys.</summary>
public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _key = "";
    private WeatherSnapshot? _last;
    private DateTimeOffset _next;
    public WeatherService(HttpClient? http = null, TimeProvider? time = null)
    {
        _ownsClient = http is null; _http = http ?? new HttpClient(); _time = time ?? TimeProvider.System;
    }
    public async Task<WeatherLocation[]> SearchAsync(string query, CancellationToken token = default)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 100 || query.Any(char.IsControl)) throw new InvalidDataException("Enter a city name of 2–100 characters.");
        using var json = await RequestAsync("https://geocoding-api.open-meteo.com/v1/search?count=6&language=en&format=json&name=" + Uri.EscapeDataString(query), token);
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid location response.");
        if (!json.RootElement.TryGetProperty("results", out var results)) return [];
        if (results.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid location response.");
        var cities = new List<WeatherLocation>();
        foreach (var item in results.EnumerateArray().Take(6))
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!TryNumber(item, "latitude", out var lat) || !TryNumber(item, "longitude", out var lon)) continue;
            var city = new WeatherLocation(Text(item, "name"), Text(item, "country"), Text(item, "admin1"), lat, lon);
            if (city.IsValid) cities.Add(city);
        }
        return cities.ToArray();
    }
    public async Task<WeatherSnapshot> SampleAsync(PulseSettings settings, CancellationToken token)
    {
        if (!settings.WeatherEnabled || settings.WeatherLocation is not { IsValid: true } city) return WeatherSnapshot.Disabled;
        var unit = settings.WeatherUnit == "Fahrenheit" ? "Fahrenheit" : "Celsius";
        var key = city.Label + "/" + city.Latitude.ToString("R", CultureInfo.InvariantCulture) + "/" + city.Longitude.ToString("R", CultureInfo.InvariantCulture) + "/" + unit;
        await _gate.WaitAsync(token);
        try
        {
            if (key != _key) { _key = key; _last = null; _next = DateTimeOffset.MinValue; }
            if (_time.GetUtcNow() < _next && _last is not null) return _last;
            try
            {
                var url = "https://api.open-meteo.com/v1/forecast?latitude=" + city.Latitude.ToString("R", CultureInfo.InvariantCulture)
                    + "&longitude=" + city.Longitude.ToString("R", CultureInfo.InvariantCulture)
                    + "&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m&wind_speed_unit=kmh&timeformat=unixtime&temperature_unit=" + unit.ToLowerInvariant();
                using var json = await RequestAsync(url, token);
                var current = json.RootElement.GetProperty("current");
                if (!TryNumber(current, "temperature_2m", out var temperature) || temperature is < -150 or > 200)
                    throw new InvalidDataException("Weather temperature unavailable.");
                var observed = DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64());
                var code = current.TryGetProperty("weather_code", out var c) && c.TryGetInt32(out var cv) ? cv : (int?)null;
                _last = new(city, temperature, TryNumber(current, "apparent_temperature", out var feels) ? feels : null,
                    TryNumber(current, "wind_speed_10m", out var wind) && wind >= 0 ? wind : null,
                    code, !current.TryGetProperty("is_day", out var day) || !day.TryGetInt32(out var dv) || dv != 0,
                    observed, unit, "Open-Meteo · current model conditions");
                _next = _time.GetUtcNow().AddMinutes(15);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { Unavailable(); }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException or FormatException)
            { Unavailable(); }
            return _last!;
            void Unavailable()
            {
                _last = _last is { Temperature: not null } old ? old with { Stale = true, Status = "Offline · last available conditions" }
                    : new(city, null, null, null, null, true, null, unit, "Weather unavailable · check connection or try again");
                _next = _time.GetUtcNow().AddMinutes(2);
            }
        }
        finally { _gate.Release(); }
    }
    private async Task<JsonDocument> RequestAsync(string url, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        const int limit = 256 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Weather response too large.");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var memory = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (memory.Length + count > limit) throw new InvalidDataException("Weather response too large.");
            memory.Write(buffer, 0, count);
        }
        return JsonDocument.Parse(memory.ToArray(), new JsonDocumentOptions { MaxDepth = 24 });
    }
    private static bool TryNumber(JsonElement root, string name, out double value)
    {
        value = 0;
        return root.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out value) && double.IsFinite(value);
    }
    private static string Text(JsonElement root, string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}
