namespace Pulse.Core;

public sealed record WeatherLocation(string Name, string Country, string Region, double Latitude, double Longitude)
{
    public string Label => string.Join(", ", new[] { Name, Region, Country }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase));
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Length <= 100 && Country is { Length: <= 100 }
        && Region is { Length: <= 100 } && !Label.Any(char.IsControl)
        && double.IsFinite(Latitude) && Latitude is >= -90 and <= 90
        && double.IsFinite(Longitude) && Longitude is >= -180 and <= 180;
}

public sealed record WeatherSnapshot(WeatherLocation? Location, double? Temperature, double? FeelsLike,
    double? WindKmh, int? Code, bool IsDay, DateTimeOffset? ObservedAt, string Unit, string Status, bool Stale = false)
{
    public static WeatherSnapshot Disabled { get; } = new(null, null, null, null, null, true, null, "Celsius", "Weather off");
    public string TemperatureText => Temperature is { } v ? $"{v:0}°{(Unit == "Fahrenheit" ? "F" : "C")}" : "—";
    public string Conditions => Code switch
    {
        0 => "Clear", 1 => "Mainly clear", 2 => "Partly cloudy", 3 => "Overcast",
        45 or 48 => "Fog", 51 or 53 or 55 => "Drizzle", 56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain", 66 or 67 => "Freezing rain", 71 or 73 or 75 or 77 => "Snow",
        80 or 81 or 82 => "Rain showers", 85 or 86 => "Snow showers", 95 or 96 or 99 => "Thunderstorm",
        _ => "Conditions unavailable"
    };
}
