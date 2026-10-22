using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class DockPresentationTests
{
    [Theory]
    [InlineData("Widget", true)]
    [InlineData("App", true)]
    [InlineData("Hidden", false)]
    public void ObsPresentationUsesOneSlotAndNeverDuplicatesTheProfileApp(string mode, bool visible)
    {
        var workspace = ProfileWorkspaces.Default(Profile.Stream) with { ObsPresentation = mode };
        Assert.Equal(visible, ProfileWorkspaces.ShowObs(workspace));
        Assert.Empty(ProfileWorkspaces.DockApps(workspace));
        // Activating a workspace can still launch its configured app.
        Assert.Single(workspace.Apps);
        var noStudio = workspace with { Widgets = [] };
        Assert.False(ProfileWorkspaces.ShowObs(noStudio));
        Assert.Equal(mode == "Hidden" ? 0 : 1, ProfileWorkspaces.DockApps(noStudio).Length);
        var withoutSlot = DockPresentation.RailExtent(workspace with { ObsPresentation = "Hidden" }, true, true, false, null);
        Assert.Equal(withoutSlot + (visible ? 36 : 0), DockPresentation.RailExtent(workspace, true, true, false, null));
    }

    [Fact]
    public void ObsModeSurvivesSavingAndUnknownModesFallBackToHoverControls()
    {
        var workspace = ProfileWorkspaces.Default(Profile.Stream) with { ObsPresentation = "App" };
        var saved = System.Text.Json.JsonSerializer.Serialize(workspace);
        Assert.Equal("App", System.Text.Json.JsonSerializer.Deserialize<ProfileWorkspace>(saved)!.Normalize().ObsPresentation);
        Assert.Equal("Widget", (workspace with { ObsPresentation = "obsolete" }).Normalize().ObsPresentation);
    }

    [Theory]
    [InlineData(0, false)][InlineData(1, false)][InlineData(2, false)][InlineData(3, true)][InlineData(16, true)]
    public void MoreThanTwoAppsShareOneFolder(int count, bool grouped) => Assert.Equal(grouped, DockPresentation.GroupApps(count));

    [Fact]
    public void BatteryNeedsBothAnEnabledWidgetAndADetectedPhysicalBattery()
    {
        var enabled = new ProfileWorkspace(Profile.Work, [], ["Battery"]);
        Assert.False(DockPresentation.ShowBattery(enabled, null));
        Assert.False(DockPresentation.ShowBattery(enabled, PowerSnapshot.Unknown));
        Assert.False(DockPresentation.ShowBattery(enabled, new(null, false, false, true)));
        Assert.True(DockPresentation.ShowBattery(enabled, new(0, true, false, false)));
        Assert.False(DockPresentation.ShowBattery(enabled with { Widgets = [] }, new(50, true, true, true)));
    }

    [Theory]
    [InlineData("Sahara Pulse", "Sahara Pulse")]
    [InlineData("  Sahara   Pulse  ", "Sahara Pulse")]
    [InlineData("One two three four five", "One two three…")]
    [InlineData("Averylongfilenamewithoutspaces1234567890", "Averylongfilenamewithout…")]
    public void CompactMediaShowsOnlyTheFirstWords(string title, string expected) => Assert.Equal(expected, DockPresentation.ShortTitle(title));

    [Fact]
    public void TitleClippingDoesNotSplitUnicodeGraphemes()
    {
        Assert.Equal("🎵🎵…", DockPresentation.ShortTitle("🎵🎵🎵", 2));
        Assert.Equal("e\u0301…", DockPresentation.ShortTitle("e\u0301x", 1));
    }

    [Fact]
    public void FullWorkspaceFitsInACompactUnscrolledRailAndLargeAppListsDoNotGrowIt()
    {
        var workspace = new ProfileWorkspace(Profile.Stream, Enumerable.Range(0, 16).Select(i => new ProfileApp("App " + i, "app" + i)).ToArray(), ProfileWorkspaces.WidgetNames);
        var desktop = DockPresentation.RailExtent(workspace, true, true, true, new(null, false, false, true));
        var laptop = DockPresentation.RailExtent(workspace, true, true, true, new(50, true, false, false));
        Assert.InRange(desktop, 350, 640); Assert.True(laptop > desktop);
        Assert.Equal(desktop, DockPresentation.RailExtent(workspace with { Apps = workspace.Apps.Take(3).ToArray() }, true, true, true, new(null, false, false, true)));
        Assert.InRange(DockPresentation.RailExtent(workspace, false, true, true, new(50, true, false, false)), 200, 480);
    }

    [Fact]
    public void InvalidCustomNotchColorsFallBackWithoutDiscardingOtherPreferences()
    {
        var settings = new PulseSettings { NotchColorMode = "Custom", NotchColor = "#00a1ff", UserName = "friend" }.Normalize();
        Assert.Equal("#00A1FF", settings.NotchColor); Assert.Equal("friend", settings.UserName);
        Assert.Equal("#D94952", (settings with { NotchColor = "invalid" }).Normalize().NotchColor);
        Assert.Equal("Theme", (settings with { NotchColorMode = "Unknown" }).Normalize().NotchColorMode);
    }
}
