using Pulse.Core;
using Xunit;
namespace Pulse.Tests;
public sealed class DockInteractionTests
{
    [Fact] public void HoverAndClicksNeverPinTheMiniBar()
    {
        var dock = new DockInteraction();
        Assert.Equal(DockStage.Resting, dock.Stage);
        dock.Hover(); dock.Click(); dock.Click();
        Assert.Equal(DockStage.Compact, dock.Stage);
        dock.Leave(); Assert.Equal(DockStage.Resting, dock.Stage);
        dock.Click(); dock.Leave(); Assert.Equal(DockStage.Resting, dock.Stage);
    }
    [Fact] public void PersistentMiniBarSurvivesLeavingAndPanelCollapse()
    {
        var dock = new DockInteraction(); dock.SetAlwaysShowMiniBar(true);
        Assert.Equal(DockStage.Compact, dock.Stage);
        dock.Leave(); Assert.Equal(DockStage.Compact, dock.Stage);
        dock.Expand(); dock.Leave(); Assert.Equal(DockStage.Expanded, dock.Stage);
        dock.Collapse(); Assert.Equal(DockStage.Compact, dock.Stage);
        dock.SetAlwaysShowMiniBar(false); Assert.Equal(DockStage.Resting, dock.Stage);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void SearchDismissalReturnsToConfiguredIdleStage(bool alwaysVisible)
    {
        var dock = new DockInteraction(); dock.SetAlwaysShowMiniBar(alwaysVisible);
        dock.Hover(); dock.Click(); dock.Collapse();
        Assert.Equal(alwaysVisible ? DockStage.Compact : DockStage.Resting, dock.Stage);
    }
    [Fact] public void ChangingThePreferenceDoesNotCloseAnExpandedWindow()
    {
        var dock = new DockInteraction(); dock.Expand();
        dock.SetAlwaysShowMiniBar(true); Assert.Equal(DockStage.Expanded, dock.Stage);
        dock.SetAlwaysShowMiniBar(false); Assert.Equal(DockStage.Expanded, dock.Stage);
        dock.Collapse(); Assert.Equal(DockStage.Resting, dock.Stage);
    }
    [Fact] public void PreferencePersistsWithoutChangingOlderSettingsDefaults()
    {
        Assert.False(System.Text.Json.JsonSerializer.Deserialize<PulseSettings>("{}")!.Normalize().AlwaysShowMiniBar);
        var saved = System.Text.Json.JsonSerializer.Serialize(new PulseSettings { AlwaysShowMiniBar = true });
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<PulseSettings>(saved)!.Normalize().AlwaysShowMiniBar);
    }
    [Theory]
    [InlineData("Bottom left", "Left edge")]
    [InlineData("Bottom center", "Right edge")]
    [InlineData("Top center", "Right edge")]
    [InlineData("Left edge", "Left edge")]
    [InlineData("Right edge", "Right edge")]
    public void ExistingAnchorsMigrateToConnectedScreenEdges(string oldAnchor, string expected)
    {
        var settings = new PulseSettings { Position = new(Anchor: oldAnchor) };
        Assert.Equal(expected, settings.Normalize().Position.Anchor);
    }
}
