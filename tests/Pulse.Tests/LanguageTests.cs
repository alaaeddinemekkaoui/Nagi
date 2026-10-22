using System.Globalization;
using System.Text;
using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class LanguageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nagi-language-" + Guid.NewGuid().ToString("N"));
    public LanguageTests() => Directory.CreateDirectory(_directory);
    private void Pack(string code, string strings) => File.WriteAllText(Path.Combine(_directory, code + ".json"),
        "{\"schemaVersion\":1,\"code\":\"" + code + "\",\"name\":\"Test\",\"nativeName\":\"Test\",\"strings\":" + strings + "}");

    [Fact]
    public void DiscoversContributorFileAndUsesSystemRegionAndEnglishFallback()
    {
        Pack("fr", "{\"Settings\":\"Paramètres\",\"Name\":\"\"}");
        var languages = new LanguageCatalog(_directory);
        languages.Select("System", "fr-CA");
        Assert.Equal("fr", languages.Selected.Code);
        Assert.Equal("Paramètres", languages.Text("Settings"));
        Assert.Equal("Name", languages.Text("Name"));
        Assert.Equal("Unknown future phrase", languages.Text("Unknown future phrase"));
        languages.Select("en", "fr-FR"); Assert.Equal("Settings", languages.Text("Settings"));
        languages.Select("xx-YY"); Assert.Equal("en", languages.Selected.Code);
    }

    [Fact]
    public void ExternalEnglishEditsApplyWithoutLosingEmbeddedFallback()
    {
        Pack("en", "{\"Settings\":\"Preferences\"}");
        var languages = new LanguageCatalog(_directory);
        Assert.Equal("Preferences", languages.Text("Settings"));
        Assert.Equal("Continue", languages.Text("Continue"));
        Assert.Equal("Settings", new LanguageCatalog(Path.Combine(_directory, "missing")).Text("Settings"));
    }

    [Fact]
    public void ReordersFormatArgumentsAndRejectsMissingOrAddedPlaceholders()
    {
        Pack("de", "{\"Version {0} for {1}\":\"Für {1}: Version {0}\"}");
        var languages = new LanguageCatalog(_directory); languages.Select("de");
        Assert.Equal("Für Nagi: Version 2", languages.Format("Version {0} for {1}", 2, "Nagi"));
        Assert.False(LanguageCatalog.SamePlaceholders("{0} and {1}", "{0}"));
        Assert.False(LanguageCatalog.SamePlaceholders("{0}", "{2}"));
        Assert.False(LanguageCatalog.SamePlaceholders("{0}", "{broken"));
        Assert.True(LanguageCatalog.SamePlaceholders("return { cpu, memory }", "retour { cpu, memory }"));
        Assert.True(LanguageCatalog.SamePlaceholders("{{{0}}}", "{{{0}}}"));
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":\"one\"}")]
    [InlineData("{\"schemaVersion\":2}")]
    public void BrokenFilesAreLoggedAndDoNotPreventStartup(string json)
    {
        File.WriteAllText(Path.Combine(_directory, "fr.json"), json);
        var failures = new List<Exception>();
        var languages = new LanguageCatalog(_directory, (_, error) => failures.Add(error));
        Assert.Single(failures); Assert.Equal("en", languages.Selected.Code);
        Assert.Equal("Settings", languages.Text("Settings"));
    }

    [Fact]
    public void InvalidPlaceholderAndDuplicateKeyPackAreIsolated()
    {
        Pack("fr", "{\"Hello {0}\":\"Bonjour\"}");
        Pack("es", "{\"Name\":\"Nombre\",\"Name\":\"Otro\"}");
        var failures = new List<Exception>(); var languages = new LanguageCatalog(_directory, (_, ex) => failures.Add(ex));
        Assert.Equal(2, failures.Count); Assert.Single(languages.Languages);
    }

    [Fact]
    public async Task LanguagePreferenceRoundTripsWithoutChangingStableSettingsValues()
    {
        using var settings = new SettingsService(_directory);
        await settings.SaveAsync(new PulseSettings { Language = "ar", Theme = "Light", LockedProfile = Profile.Work });
        var loaded = await settings.LoadAsync();
        Assert.Equal("ar", loaded.Language); Assert.Equal("Light", loaded.Theme); Assert.Equal(Profile.Work, loaded.LockedProfile);
        Assert.Equal("System", (loaded with { Language = "../bad.json" }).Normalize().Language);
    }

    [Fact]
    public void AllBundledLanguagesLoadAndExposeRealTranslationsAndArabicDirection()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "languages"))) root = root.Parent;
        Assert.NotNull(root);
        var failures = new List<string>();
        var languages = new LanguageCatalog(Path.Combine(root!.FullName, "languages"), (path, ex) => failures.Add(Path.GetFileName(path) + ": " + ex.Message));
        Assert.Empty(failures);
        foreach (var code in new[] { "ar", "de", "en", "es", "fr", "ru" })
            Assert.Contains(languages.Languages, language => language.Code == code);
        foreach (var code in languages.Languages.Where(language => language.Code != "en").Select(language => language.Code))
        {
            languages.Select(code); Assert.NotEqual("Choose your language.", languages.Text("Choose your language."));
            Assert.DoesNotContain("{0}", languages.Format("Current language: {0}", "Nagi"));
            var englishKeys = languages.Languages.Single(l => l.Code == "en").Strings.Keys;
            Assert.Equal(englishKeys.Order(), languages.Selected.Strings.Keys.Order());
        }
        languages.Select("ar-EG"); Assert.True(languages.Selected.RightToLeft);
        languages.Select("ru-RU"); Assert.False(languages.Selected.RightToLeft);
    }

    [Fact]
    public void ChineseSystemRegionsResolveToTheirScriptPacks()
    {
        Pack("zh-Hans", "{\"Settings\":\"设置\"}");
        Pack("zh-Hant", "{\"Settings\":\"設定\"}");
        var languages = new LanguageCatalog(_directory);
        languages.Select("System", "zh-CN"); Assert.Equal("zh-Hans", languages.Selected.Code);
        languages.Select("System", "zh-TW"); Assert.Equal("zh-Hant", languages.Selected.Code);
        languages.Select("System", "zh-HK"); Assert.Equal("zh-Hant", languages.Selected.Code);
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
