using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class GameDetectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T12:00:00Z");
    private static readonly ForegroundApplication Game = new(42, "game", @"D:\SteamLibrary\steamapps\common\Game\bin\game.exe") { IsGame = true, GameSource = "Steam" };
    private static readonly GameLibrary[] Libraries = [new(@"D:\SteamLibrary\steamapps\common", "Steam"), new(@"E:\Epic\Game", "Epic Games")];

    [Theory]
    [InlineData(@"d:\steamlibrary\steamapps\common\Game\bin\Game.EXE", "Steam")]
    [InlineData("E:/Epic/Game/Binaries/game.exe", "Epic Games")]
    [InlineData(@"E:\Epic\GameTwo\game.exe", null)]
    [InlineData(@"D:\SteamLibrary\steam.exe", null)]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Tools\obs64.exe", null)]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\CrashReportClient.exe", null)]
    [InlineData(@"E:\Epic\Game\UnrealEditor.exe", null)]
    [InlineData(@"E:\Epic\Game\chrome.exe", null)]
    [InlineData(@"E:\Epic\Game\game.dll", null)]
    [InlineData(null, null)]
    public void LibraryDetectionUsesDirectoryBoundariesAndExcludesNonGameExecutables(string? executable, string? expected)
        => Assert.Equal(expected, GameLibraries.SourceFor(executable, Libraries));

    [Fact] public void RecognizedGameActivatesAfterDebounceAndReturnsToDefaultOnAppSwitch()
    {
        var engine = new ContextEngine(); var settings = new PulseSettings { AutomaticGameDetection = true, Rules = [] };
        Assert.Equal(Profile.Default, engine.Evaluate(Game, settings, Now));
        Assert.Equal(Profile.Gaming, engine.Evaluate(Game, settings, Now.AddSeconds(2)));
        var browser = new ForegroundApplication(43, "chrome", @"C:\Apps\chrome.exe", true);
        Assert.Equal(Profile.Gaming, engine.Evaluate(browser, settings, Now.AddSeconds(3)));
        Assert.Equal(Profile.Default, engine.Evaluate(browser, settings, Now.AddSeconds(5)));
    }
    [Fact] public void DetectionDoesNotOverrideExecutableRulesOrManualLock()
    {
        var settings = new PulseSettings { AutomaticGameDetection = true, Rules = [new("game.exe", Profile.Work)] };
        var engine = new ContextEngine(); engine.Evaluate(Game, settings, Now);
        Assert.Equal(Profile.Work, engine.Evaluate(Game, settings, Now.AddSeconds(2)));
        Assert.Equal(Profile.Focus, engine.Evaluate(Game, settings with { LockedProfile = Profile.Focus }, Now.AddSeconds(4)));
        Assert.Equal(Profile.Default, engine.Evaluate(Game, settings with { AutomaticSwitching = false }, Now.AddSeconds(5)));
    }
    [Fact] public void DisabledDetectionAndFullscreenVideoDoNotTriggerGaming()
    {
        var engine = new ContextEngine(); var settings = new PulseSettings { Rules = [] };
        engine.Evaluate(Game, settings, Now);
        Assert.Equal(Profile.Default, engine.Evaluate(Game, settings, Now.AddSeconds(2)));
        var video = new ForegroundApplication(52, "video", @"C:\Player\player.exe", true);
        engine.Evaluate(video, settings with { AutomaticGameDetection = true }, Now.AddSeconds(3));
        Assert.Equal(Profile.Default, engine.Evaluate(video, settings with { AutomaticGameDetection = true }, Now.AddSeconds(5)));
    }
    [Theory] [InlineData(true, 52)] [InlineData(false, 42)]
    public void FpsSlotReservesSpaceOnlyWithPerformanceWidget(bool horizontal, double extra)
    {
        var workspace = ProfileWorkspaces.Default(Profile.Gaming);
        var baseline = DockPresentation.RailExtent(workspace, horizontal, true, false, null);
        Assert.Equal(baseline + extra, DockPresentation.RailExtent(workspace, horizontal, true, false, null, showFps: true));
        var focus = workspace with { Widgets = ["Media"] };
        Assert.Equal(DockPresentation.RailExtent(focus, horizontal, true, false, null), DockPresentation.RailExtent(focus, horizontal, true, false, null, showFps: true));
    }
    [Fact] public async Task GamingPreferencesRoundTripIndependently()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NagiGameSettings", Guid.NewGuid().ToString("N"));
        try
        {
            using var service = new SettingsService(directory);
            await service.SaveAsync(new() { AutomaticGameDetection = true, FpsEnabled = false, LockedProfile = Profile.Focus });
            var settings = await service.LoadAsync();
            Assert.True(settings.AutomaticGameDetection); Assert.False(settings.FpsEnabled); Assert.Equal(Profile.Focus, settings.LockedProfile);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
