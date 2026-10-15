using Pulse.Infrastructure;
using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class LuaUiTests
{
    [Fact]
    public void NagiAndLegacyLuaContextsStayCompatible()
    {
        var style = LuaUiService.Evaluate("return { name = nagi == pulse and 'Shared context' or 'Different', labels = { cpu = nagi.theme .. ' ' .. pulse.profile } }", "Akeno", "Focus");
        Assert.Equal("Shared context", style.Name);
        Assert.Equal("Akeno Focus", style.CpuLabel);
        Assert.Equal(LuaUiService.Evaluate("return { name = pulse.profile }", "Dark", "Gaming"),
            LuaUiService.Evaluate("return { name = nagi.profile }", "Dark", "Gaming"));
    }

    [Theory]
    [InlineData("Dark", "Work")]
    [InlineData("Light", "Gaming")]
    [InlineData("Dark", "Focus")]
    public void PresetsRunWithThemeAndProfile(string theme, string profile)
    {
        foreach (var source in LuaUiService.Presets.Values)
            Assert.NotNull(LuaUiService.Evaluate(source, theme, profile).Accent);
        var quiet = LuaUiService.Evaluate(LuaUiService.Presets["Quiet focus"], theme, profile);
        Assert.Equal(profile != "Focus", quiet.ShowMedia);
    }
    [Theory]
    [InlineData("return nil")]
    [InlineData("return { accidental_typo = true }")]
    [InlineData("return { sections = { media = 1 } }")]
    [InlineData("return { sections = { typo = true } }")]
    [InlineData("return { card_radius = 100 }")]
    [InlineData("return { spacing = 0/0 }")]
    [InlineData("return { accent = '#00FFFFFF' }")]
    [InlineData("return { surface = '#GGGGGG' }")]
    [InlineData("return { labels = { cpu = 'bad\\nlabel' } }")]
    [InlineData("return { labels = false }")]
    [InlineData("return {")]
    public void RejectsInvalidAppearanceAndReportsErrors(string source) =>
        Assert.Throws<InvalidDataException>(() => LuaUiService.Evaluate(source, "Dark", "Work"));

    [Theory]
    [InlineData("while true do end")]
    [InlineData("local function f() return f() end return f()")]
    public void InstructionBudgetStopsInfiniteScripts(string source) =>
        Assert.Contains("budget", Assert.Throws<InvalidDataException>(() => LuaUiService.Evaluate(source, "Dark", "Work")).Message);

    [Theory]
    [InlineData("os", "execute")]
    [InlineData("io", "open")]
    [InlineData("debug", "getregistry")]
    [InlineData("clr", "load")]
    [InlineData("string", "rep")]
    [InlineData("coroutine", "create")]
    public void HostAndUnboundedLibraryFunctionsAreUnavailable(string module, string method) =>
        Assert.Throws<InvalidDataException>(() => LuaUiService.Evaluate($"return {module}.{method}('x')", "Dark", "Work"));

    [Fact] public void EmptyTableRestoresDefaultsAndArithmeticWorks()
    {
        Assert.Equal(new LuaUiStyle(), LuaUiService.Evaluate("return {}", "Dark", "Work"));
        Assert.Equal(16, LuaUiService.Evaluate("return { spacing = math.min(8 * 2, 20) }", "Dark", "Work").Spacing);
        Assert.Throws<InvalidDataException>(() => LuaUiService.Evaluate(new string(' ', LuaUiService.MaximumBytes + 1), "Dark", "Work"));
    }

    [Fact] public async Task FilesRoundTripBackupAndDoNotOverwriteExistingScripts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseLuaTests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new LuaUiService(directory);
            Assert.Equal(LuaUiService.Presets["Rose glass"], await service.ReadAsync());
            Assert.False(File.Exists(service.FilePath));
            await service.SaveAsync("return {}");
            await service.SaveAsync("return { spacing = 12 }");
            Assert.Equal("return { spacing = 12 }", await service.ReadAsync());
            Assert.Equal("return {}", await File.ReadAllTextAsync(service.FilePath + ".bak"));
            await File.WriteAllTextAsync(service.FilePath, new string('x', LuaUiService.MaximumBytes + 1));
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact] public async Task ScriptingOptInPersistsWithoutChangingOtherPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseLuaTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var settings = new SettingsService(directory);
            Assert.False((await settings.LoadAsync()).LuaUiEnabled);
            await settings.SaveAsync(new PulseSettings { LuaUiEnabled = true, Theme = "Light", ShowMedia = false });
            var loaded = await settings.LoadAsync();
            Assert.True(loaded.LuaUiEnabled); Assert.Equal("Light", loaded.Theme); Assert.False(loaded.ShowMedia);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
