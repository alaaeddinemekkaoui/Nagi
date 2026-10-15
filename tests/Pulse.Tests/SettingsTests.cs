using System.Text.Json.Nodes;
using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PulseTests", Guid.NewGuid().ToString("N"));
    private readonly SettingsService _service;
    public SettingsTests() { Directory.CreateDirectory(_directory); _service = new(_directory); }
    private Task Write(string text) => File.WriteAllTextAsync(_service.FilePath, text);
    [Fact] public async Task MissingFileReturnsSafeDefaults()
    {
        var settings = await _service.LoadAsync();
        Assert.Equal("Dark", settings.Theme); Assert.False(settings.StartWithWindows); Assert.False(settings.StartExpanded);
    }
    [Fact] public async Task RoundTripPersistsAllUserConfiguration()
    {
        var settings = new PulseSettings { Theme = "Light", LockedProfile = Profile.Gaming, PollingIntervalMs = 3000, HideHotkey = new(6, 0x48), GlassEnabled = false,
            Position = new("display", "Left edge", 20, -10), Hotkey = new(6, 0x41),
            Rules = [new("mygame.exe", Profile.Gaming, 200)], QuickActions = [new("Site", ActionKind.Url, "https://example.com", Profile.Work)] };
        await _service.SaveAsync(settings);
        var loaded = await _service.LoadAsync();
        Assert.Equal(settings.Theme, loaded.Theme); Assert.Equal(settings.Hotkey, loaded.Hotkey);
        Assert.Equal(settings.HideHotkey, loaded.HideHotkey); Assert.Equal(settings.GlassEnabled, loaded.GlassEnabled);
        Assert.Equal(settings.LockedProfile, loaded.LockedProfile); Assert.Equal(settings.Position, loaded.Position);
        Assert.Equal(settings.Rules, loaded.Rules); Assert.Equal(settings.QuickActions, loaded.QuickActions);
    }
    [Fact] public async Task InvalidFieldDoesNotEraseValidSiblingsOrFutureFields()
    {
        await Write("""{"Theme":"Light","PollingIntervalMs":"broken","LockedProfile":"Gaming","FutureFeature":{"enabled":true}}""");
        var settings = await _service.LoadAsync();
        Assert.Equal("Light", settings.Theme); Assert.Equal(Profile.Gaming, settings.LockedProfile); Assert.Equal(1500, settings.PollingIntervalMs);
        Assert.NotNull(_service.RecoveryMessage);
        await _service.SaveAsync(settings with { Notifications = false });
        var json = JsonNode.Parse(await File.ReadAllTextAsync(_service.FilePath))!;
        Assert.True(json["FutureFeature"]!["enabled"]!.GetValue<bool>());
        Assert.Single(Directory.GetFiles(_directory, "*.recovered.*"));
    }
    [Fact] public async Task CorruptJsonIsPreservedBeforeRecovery()
    {
        await Write("{ broken json");
        var settings = await _service.LoadAsync();
        Assert.Equal("Dark", settings.Theme); Assert.NotNull(_service.RecoveryMessage);
        Assert.Equal("{ broken json", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_directory, "*.corrupt.*"))));
        await _service.SaveAsync(settings);
        Assert.NotNull(JsonNode.Parse(await File.ReadAllTextAsync(_service.FilePath)));
    }
    [Fact] public async Task FutureSchemaIsReadOnly()
    {
        const string original = """{"SchemaVersion":99,"Theme":"Light","FutureFeature":42}""";
        await Write(original); var settings = await _service.LoadAsync();
        Assert.Equal("Light", settings.Theme);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(settings));
        Assert.Equal(original, await File.ReadAllTextAsync(_service.FilePath));
    }
    [Fact] public async Task NormalizationClampsInvalidValuesAndHandlesNulls()
    {
        await Write("""{"PollingIntervalMs":1,"Theme":"invalid","DefaultProfile":999,"Hotkey":null,"Rules":null,"QuickActions":null,"Position":{"Anchor":"lost","OffsetX":1e10}}""");
        var settings = await _service.LoadAsync();
        Assert.Equal(500, settings.PollingIntervalMs); Assert.Equal("Dark", settings.Theme); Assert.Equal(Profile.Work, settings.DefaultProfile);
        Assert.NotNull(settings.Hotkey); Assert.Equal("Right edge", settings.Position.Anchor); Assert.InRange(settings.Position.OffsetX, -100000, 100000);
    }
    [Fact] public async Task SavesAreAtomicAndLeavePreviousBackup()
    {
        await _service.SaveAsync(new() { Theme = "Light" });
        await _service.SaveAsync(new() { Theme = "Dark" });
        Assert.Equal("Light", JsonNode.Parse(await File.ReadAllTextAsync(_service.FilePath + ".bak"))!["Theme"]!.GetValue<string>());
        Assert.False(File.Exists(_service.FilePath + ".tmp"));
    }
    [Fact] public async Task LockedSettingsRecoverWithoutCrashingOrOverwritingOriginal()
    {
        await Write("""{"Theme":"Light"}""");
        using (var locked = new FileStream(_service.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var settings = await _service.LoadAsync();
            Assert.Equal("Dark", settings.Theme);
            Assert.Contains("read-only", _service.RecoveryMessage);
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(settings));
        }
        Assert.Equal("Light", JsonNode.Parse(await File.ReadAllTextAsync(_service.FilePath))!["Theme"]!.GetValue<string>());
    }
    [Fact] public async Task LegacyCasingIsReadWithoutDuplicateKeysOnSave()
    {
        await Write("""{"theme":"Light","pollingIntervalMs":2500}""");
        var settings = await _service.LoadAsync(); await _service.SaveAsync(settings);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(_service.FilePath))!.AsObject();
        Assert.Single(json, p => p.Key.Equals("theme", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Light", settings.Theme);
    }
    public void Dispose() { _service.Dispose(); Directory.Delete(_directory, true); }
}
