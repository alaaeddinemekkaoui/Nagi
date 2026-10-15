using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class DockInteractionTests
{
    [Fact] public void HoverRevealsCompactAndLeavingReturnsToTheEdge()
    {
        var dock = new DockInteraction();
        Assert.Equal(DockStage.Resting, dock.Stage);
        dock.Hover(); Assert.Equal(DockStage.Compact, dock.Stage); Assert.False(dock.Pinned);
        dock.Leave(); Assert.Equal(DockStage.Resting, dock.Stage);
    }
    [Fact] public void FirstClickPinsTheHoverRevealAndSecondClickExpands()
    {
        var dock = new DockInteraction(); dock.Hover(); dock.Click(); dock.Leave();
        Assert.Equal(DockStage.Compact, dock.Stage); Assert.True(dock.Pinned);
        dock.Click(); Assert.Equal(DockStage.Expanded, dock.Stage);
        dock.Leave(); Assert.Equal(DockStage.Expanded, dock.Stage);
    }
    [Fact] public void ClickingTheRestingDropAlsoRequiresTwoClicksToExpand()
    {
        var dock = new DockInteraction(); dock.Click();
        Assert.Equal(DockStage.Compact, dock.Stage); Assert.True(dock.Pinned);
        dock.Click(); Assert.Equal(DockStage.Expanded, dock.Stage);
    }
    [Fact] public void ExplicitDismissalClearsPinnedAndExpandedState()
    {
        var dock = new DockInteraction(); dock.Click(); dock.Collapse();
        dock.Hover(); Assert.False(dock.Pinned);
        dock.Expand(); dock.Collapse(); Assert.Equal(DockStage.Resting, dock.Stage);
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
