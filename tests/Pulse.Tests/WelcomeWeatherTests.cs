using System.Globalization;
using System.Net;
using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class WelcomeWeatherTests
{
    private static readonly WeatherLocation Tokyo = new("Tokyo", "Japan", "Tokyo", 35.69, 139.69);
    private const string Current = """{"current":{"time":1800000000,"temperature_2m":21.5,"apparent_temperature":20,"weather_code":2,"is_day":1,"wind_speed_10m":12}}""";
    private static PulseSettings Enabled => new() { WeatherEnabled = true, WeatherLocation = Tokyo };
    [Theory]
    [InlineData("Vertical", "Left edge", "Left edge")]
    [InlineData("Vertical", "Top edge", "Right edge")]
    [InlineData("Horizontal", "Top edge", "Top edge")]
    [InlineData("Horizontal", "Bottom edge", "Bottom edge")]
    [InlineData("Horizontal", "Left edge", "Bottom edge")]
    public void OrientationAndEdgeNormalizeTogether(string orientation, string input, string expected)
    {
        var normalized = new PulseSettings { Position = new(Anchor: input, Orientation: orientation) }.Normalize();
        Assert.Equal(orientation, normalized.Position.Orientation); Assert.Equal(expected, normalized.Position.Anchor);
    }
    [Fact] public void BadWeatherAndDisplayNameAreNormalizedWithoutEnablingNetwork()
    {
        var normalized = (Enabled with { UserName = "  A\nB  ", WeatherLocation = Tokyo with { Latitude = double.NaN }, WeatherUnit = "unknown" }).Normalize();
        Assert.Equal("AB", normalized.UserName); Assert.False(normalized.WeatherEnabled); Assert.Null(normalized.WeatherLocation); Assert.Equal("Celsius", normalized.WeatherUnit);
        Assert.False(new PulseSettings().WelcomeCompleted); Assert.False(new PulseSettings().WeatherEnabled);
    }
    [Fact] public async Task WelcomeAndWeatherPersistWithExistingRulesAndLegacyScriptOptIn()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NagiWelcome", Guid.NewGuid().ToString("N"));
        try
        {
            using var settings = new SettingsService(directory); await settings.LoadAsync();
            var value = Enabled with { WelcomeCompleted = true, UserName = "Alex", Theme = "Akeno", LuaUiEnabled = true, Position = new(Anchor: "Top edge", Orientation: "Horizontal") };
            await settings.SaveAsync(value); var loaded = await settings.LoadAsync();
            Assert.True(loaded.WelcomeCompleted); Assert.Equal("Alex", loaded.UserName); Assert.Equal(Tokyo, loaded.WeatherLocation);
            Assert.Equal("Horizontal", loaded.Position.Orientation); Assert.Equal("Top edge", loaded.Position.Anchor);
            Assert.True(loaded.LuaUiEnabled); Assert.Equal(value.Rules, loaded.Rules);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public async Task DisabledWeatherMakesNoRequests()
    {
        var handler = new Handler(_ => new(HttpStatusCode.InternalServerError)); using var client = new HttpClient(handler); using var weather = new WeatherService(client);
        Assert.Equal(WeatherSnapshot.Disabled, await weather.SampleAsync(new(), default)); Assert.Equal(0, handler.Calls);
    }
    [Fact] public async Task SkippedWeatherNullLocationReloadsWithoutRecoveryWarning()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NagiWelcome", Guid.NewGuid().ToString("N"));
        try
        {
            using (var writer = new SettingsService(directory))
                await writer.SaveAsync(new PulseSettings { WelcomeCompleted = true, UserName = "Alex" });
            using var reader = new SettingsService(directory);
            var loaded = await reader.LoadAsync();
            Assert.True(loaded.WelcomeCompleted); Assert.Null(loaded.WeatherLocation); Assert.False(loaded.WeatherEnabled);
            Assert.Null(reader.RecoveryMessage);
            Assert.Empty(Directory.GetFiles(directory, "settings.json.recovered.*"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public async Task SearchEncodesNamesAndRejectsInvalidCoordinates()
    {
        var handler = new Handler(request =>
        {
            Assert.Equal("geocoding-api.open-meteo.com", request.RequestUri!.Host);
            Assert.Contains("name=Paris%2C%20FR%20%26%20x", request.RequestUri.AbsoluteUri);
            return Json("""{"results":[{"name":"Paris","country":"France","admin1":"Ile","latitude":48.85,"longitude":2.35},{"name":"Bad","latitude":999,"longitude":0},null]}""");
        });
        using var client = new HttpClient(handler); using var weather = new WeatherService(client);
        var result = await weather.SearchAsync("Paris, FR & x"); Assert.Single(result); Assert.Equal("Paris, Ile, France", result[0].Label);
    }
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Paris\nFrance")]
    public async Task InvalidSearchDoesNotContactProvider(string query)
    {
        var handler = new Handler(_ => Json("{}")); using var client = new HttpClient(handler); using var weather = new WeatherService(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => weather.SearchAsync(query)); Assert.Equal(0, handler.Calls);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"results\":{}}")]
    public async Task MalformedLocationResultsAreReported(string body)
    {
        using var client = new HttpClient(new Handler(_ => Json(body))); using var weather = new WeatherService(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => weather.SearchAsync("Tokyo"));
    }
    [Fact] public async Task OversizedLocationResponseIsBounded()
    {
        using var client = new HttpClient(new Handler(_ => Json(new string(' ', 300000)))); using var weather = new WeatherService(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => weather.SearchAsync("Tokyo"));
    }
    [Fact] public async Task ConditionsAreCachedAndCityChangesInvalidateThem()
    {
        var handler = new Handler(_ => Json(Current)); using var client = new HttpClient(handler); using var weather = new WeatherService(client);
        var first = await weather.SampleAsync(Enabled, default); var second = await weather.SampleAsync(Enabled, default);
        Assert.Equal(21.5, first.Temperature); Assert.Equal("Partly cloudy", first.Conditions); Assert.Same(first, second); Assert.Equal(1, handler.Calls);
        var changed = await weather.SampleAsync(Enabled with { WeatherLocation = Tokyo with { Name = "Kyoto", Latitude = 35.01, Longitude = 135.77 } }, default);
        Assert.Equal("Kyoto", changed.Location!.Name); Assert.Equal(2, handler.Calls);
    }
    [Fact] public async Task FahrenheitRequestsAndCoordinatesUseInvariantFormatting()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var client = new HttpClient(new Handler(request => { Assert.Contains("latitude=35.69", request.RequestUri!.Query); Assert.Contains("temperature_unit=fahrenheit", request.RequestUri.Query); return Json(Current); }));
            using var weather = new WeatherService(client); var result = await weather.SampleAsync(Enabled with { WeatherUnit = "Fahrenheit" }, default);
            Assert.EndsWith("°F", result.TemperatureText);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"current\":{\"temperature_2m\":null}}")]
    [InlineData("{\"current\":{\"temperature_2m\":999}}")]
    public async Task InvalidConditionsStayUnavailableRatherThanBecomingZero(string body)
    {
        using var client = new HttpClient(new Handler(_ => Json(body))); using var weather = new WeatherService(client);
        var result = await weather.SampleAsync(Enabled, default); Assert.Null(result.Temperature); Assert.Equal("—", result.TemperatureText);
    }
    [Fact] public async Task FailureKeepsLastReadingWithAnExplicitStaleStateAndBackoff()
    {
        var time = new Clock(); var calls = 0;
        using var client = new HttpClient(new Handler(_ => ++calls == 1 ? Json(Current) : new(HttpStatusCode.ServiceUnavailable)));
        using var weather = new WeatherService(client, time);
        await weather.SampleAsync(Enabled, default); time.Now = time.Now.AddMinutes(16);
        var stale = await weather.SampleAsync(Enabled, default); Assert.True(stale.Stale); Assert.Equal(21.5, stale.Temperature);
        await weather.SampleAsync(Enabled, default); Assert.Equal(2, calls);
    }
    [Fact] public async Task CallerCancellationIsNotConvertedToAnOfflineReading()
    {
        using var client = new HttpClient(new Handler(_ => Json(Current))); using var weather = new WeatherService(client);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => weather.SampleAsync(Enabled, cancel.Token));
    }
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, System.Text.Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(respond(request)); }
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
}
