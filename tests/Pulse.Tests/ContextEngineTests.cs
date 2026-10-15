using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class ContextEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
    private static readonly ForegroundApplication Game = new(123, "game", @"C:\Games\game.exe", true);
    [Fact] public void ManualLockOverridesApplicationRules()
    {
        var engine = new ContextEngine();
        var settings = new PulseSettings { LockedProfile = Profile.Focus, Rules = [new("game.exe", Profile.Gaming, 9999)] };
        Assert.Equal(Profile.Focus, engine.Evaluate(Game, settings, Now));
        Assert.Equal(Profile.Focus, engine.Evaluate(Game, settings, Now.AddMinutes(1)));
    }
    [Fact] public void AutomaticRulesChooseHighestPriorityAfterDebounce()
    {
        var engine = new ContextEngine();
        var settings = new PulseSettings { Rules = [new("game.exe", Profile.Focus, 10), new("game.exe", Profile.Gaming, 20)] };
        Assert.Equal(Profile.Work, engine.Evaluate(Game, settings, Now));
        Assert.Equal(Profile.Work, engine.Evaluate(Game, settings, Now.AddSeconds(1)));
        Assert.Equal(Profile.Gaming, engine.Evaluate(Game, settings, Now.AddSeconds(2)));
    }
    [Fact] public void RapidForegroundChangesDoNotSwitchProfiles()
    {
        var engine = new ContextEngine();
        var settings = new PulseSettings { Rules = [new("game.exe", Profile.Gaming)] };
        engine.Evaluate(Game, settings, Now);
        engine.Evaluate(new(1, "editor", null), settings, Now.AddSeconds(1));
        Assert.Equal(Profile.Work, engine.Evaluate(Game, settings, Now.AddMilliseconds(1500)));
        Assert.Equal(Profile.Work, engine.Evaluate(Game, settings, Now.AddSeconds(2)));
        Assert.Equal(Profile.Gaming, engine.Evaluate(Game, settings, Now.AddSeconds(4)));
    }
    [Fact] public void DisablingAutomaticUsesFallbackImmediately()
    {
        Assert.Equal(Profile.Focus, new ContextEngine().Evaluate(Game, new() { AutomaticSwitching = false, DefaultProfile = Profile.Focus }, Now));
    }
    [Fact] public void FullscreenAloneDoesNotClassifyGame()
    {
        var engine = new ContextEngine(); var settings = new PulseSettings { Rules = [] };
        engine.Evaluate(Game, settings, Now);
        Assert.Equal(Profile.Work, engine.Evaluate(Game, settings, Now.AddMinutes(1)));
    }
    [Fact] public void DisabledRulesAreIgnoredAndTiesUseStableOrder()
    {
        var settings = new PulseSettings { Rules = [new("game.exe", Profile.Focus, 999, false), new("game.exe", Profile.Gaming, 100), new("game.exe", Profile.Focus, 100)] };
        var engine = new ContextEngine(); engine.Evaluate(Game, settings, Now);
        Assert.Equal(Profile.Gaming, engine.Evaluate(Game, settings, Now.AddSeconds(3)));
    }
    [Theory]
    [InlineData("GAME.EXE", true)] [InlineData("game", true)] [InlineData(@"c:\games\GAME.EXE", true)]
    [InlineData(@"C:\Other\game.exe", false)] [InlineData("gam", false)] [InlineData("", false)]
    public void RulesMatchExactNamesOrFullPaths(string identity, bool expected) => Assert.Equal(expected, ContextEngine.Matches(identity, Game));
}
