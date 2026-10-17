using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class ProfileWorkspaceTests
{
    [Fact] public void DefaultsProvideDistinctWorkAndGamingDocksWithNoAutomaticLaunch()
    {
        var settings = new PulseSettings();
        Assert.Equal("System", settings.Theme); Assert.True(settings.GlassEnabled);
        Assert.Equal(["code", "terminal", "browser"], settings.Workspace(Profile.Work).Apps.Select(a => a.Target));
        Assert.Equal(["steam", "browser"], settings.Workspace(Profile.Gaming).Apps.Select(a => a.Target));
        Assert.All(settings.Workspaces, w => Assert.False(w.AutoOpenApps));
        Assert.False(settings.Workspace(Profile.Focus).Shows("Performance"));
        Assert.True(settings.Workspace(Profile.Work).Shows("Microphone"));
    }
    [Fact] public void DefaultStartsEmptyAndStreamAddsStudioWithoutChangingExistingEnumValues()
    {
        var settings = new PulseSettings();
        Assert.Equal(Profile.Default, settings.DefaultProfile);
        Assert.Empty(settings.Workspace(Profile.Default).Apps);
        Assert.Equal(["Performance", "Media"], settings.Workspace(Profile.Default).Widgets);
        Assert.True(settings.Workspace(Profile.Stream).Shows("Studio"));
        Assert.False(settings.Workspace(Profile.Work).Shows("Studio"));
        Assert.All(new[] { Profile.Work, Profile.Gaming, Profile.Stream }, profile =>
        {
            foreach (var optional in new[] { "Microphone", "Weather", "Battery" }) Assert.True(settings.Workspace(profile).Shows(optional));
        });
        Assert.Equal(0, (int)Profile.Work); Assert.Equal(1, (int)Profile.Gaming); Assert.Equal(2, (int)Profile.Focus);
    }
    [Fact] public async Task MusicUpdatesScenesAndOptionalWidgetsRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NagiSettingsTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var service = new SettingsService(directory);
            var settings = new PulseSettings { PreferredMusicPlayer = "Spotify", AutomaticUpdates = false, ObsScenes = ["Starting", "Game", "Away"] };
            settings = settings with { Workspaces = settings.Workspaces.Select(w => w.Profile == Profile.Work ? w with { Widgets = ["Media"] } : w).ToArray() };
            await service.SaveAsync(settings); var loaded = await service.LoadAsync();
            Assert.Equal("Spotify", loaded.PreferredMusicPlayer); Assert.False(loaded.AutomaticUpdates);
            Assert.Equal(settings.ObsScenes, loaded.ObsScenes); Assert.Equal(["Media"], loaded.Workspace(Profile.Work).Widgets);
            Assert.Equal(3, MusicPlayers.For("All").Count()); Assert.Equal("Spotify", Assert.Single(MusicPlayers.For(loaded.PreferredMusicPlayer)).Name);
            Assert.Equal("All", (new PulseSettings { PreferredMusicPlayer = "unknown", ObsScenes = null! }).Normalize().PreferredMusicPlayer);
            Assert.Equal(["", "", ""], (new PulseSettings { ObsScenes = null! }).Normalize().ObsScenes);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public void OldAndMalformedWorkspacesRecoverWithoutErasingAnIntentionallyEmptyDock()
    {
        var settings = new PulseSettings { Workspaces = [new(Profile.Work, [], [], true), new(Profile.Work, [new("Other", "anything")], ["Media"]), new((Profile)999, [], [])] }.Normalize();
        Assert.Equal(5, settings.Workspaces.Length);
        Assert.Empty(settings.Workspace(Profile.Work).Apps); Assert.Empty(settings.Workspace(Profile.Work).Widgets);
        Assert.True(settings.Workspace(Profile.Work).AutoOpenApps);
        Assert.NotEmpty(settings.Workspace(Profile.Gaming).Apps);
    }
    [Fact] public void UnknownWidgetAndAppKindsCannotIntroduceCommands()
    {
        var workspace = new ProfileWorkspace(Profile.Work,
            [new("Command", "cmd.exe /c test", "Command"), new("Invalid", "unknown", "Suggested"), new("Code", "code", "Suggested"), new("Code again", "code", "Suggested")],
            ["Media", "Unknown", "Media"]).Normalize();
        Assert.Single(workspace.Apps); Assert.Equal("code", workspace.Apps[0].Target);
        Assert.Equal(["Media"], workspace.Widgets);
    }
    [Fact] public void SuggestedAppResolvesOnlyAgainstAnInstalledApplication()
    {
        var suggestion = new ProfileApp("Editor", "code", "Suggested");
        var installed = new SearchEntry("app:code", "Visual Studio Code", "Installed app", SearchKind.Application, "catalog-code");
        Assert.Null(ProfileWorkspaces.Resolve(suggestion, []));
        Assert.Null(ProfileWorkspaces.Resolve(suggestion, [installed with { Kind = SearchKind.Command }]));
        Assert.Same(installed, ProfileWorkspaces.Resolve(suggestion, [installed]));
        Assert.Null(ProfileWorkspaces.Resolve(new("Bad", "untrusted shell string"), [installed]));
    }
    [Fact] public async Task WorkspacesAndBackgroundRoundTripWithoutChangingOtherProfiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NagiWorkspaceTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var service = new SettingsService(directory);
            var settings = new PulseSettings { AkenoBackground = "Gradient" };
            settings = settings with { Workspaces = settings.Workspaces.Select(w => w.Profile == Profile.Work ? w with { Apps = [new("My app", "catalog-app")], Widgets = ["Media", "Microphone"], AutoOpenApps = true } : w).ToArray() };
            await service.SaveAsync(settings); var loaded = await service.LoadAsync();
            Assert.Equal("Gradient", loaded.AkenoBackground);
            Assert.True(loaded.Workspace(Profile.Work).AutoOpenApps);
            Assert.Equal("catalog-app", Assert.Single(loaded.Workspace(Profile.Work).Apps).Target);
            Assert.False(loaded.Workspace(Profile.Work).Shows("Performance"));
            Assert.Equal(settings.Workspace(Profile.Gaming).Apps, loaded.Workspace(Profile.Gaming).Apps);
            Assert.Null(service.RecoveryMessage);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public void ExplicitConsentDoesNotLaunchOnBackgroundDetectionStartupOrRefresh()
    {
        var workspace = ProfileWorkspaces.Default(Profile.Work) with { AutoOpenApps = true };
        Assert.False(ProfileWorkspaces.ShouldOpenApps(Profile.Work, workspace, false, false));
        Assert.False(ProfileWorkspaces.ShouldOpenApps(null, workspace, true, false));
        Assert.False(ProfileWorkspaces.ShouldOpenApps(Profile.Gaming, workspace, true, false));
        Assert.False(ProfileWorkspaces.ShouldOpenApps(Profile.Work, workspace, true, true));
        Assert.True(ProfileWorkspaces.ShouldOpenApps(Profile.Work, workspace, true, false));
        Assert.False(ProfileWorkspaces.ShouldOpenApps(Profile.Work, workspace with { AutoOpenApps = false }, true, false));
    }
    [Theory][InlineData("Samurai")][InlineData("Gradient")][InlineData("None")]
    public void BackgroundModesRemainOptional(string mode) => Assert.Equal(mode, new PulseSettings { AkenoBackground = mode }.Normalize().AkenoBackground);
}
