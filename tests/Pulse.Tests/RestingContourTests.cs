using System.Numerics;
using Pulse.Core;
using Pulse.Core.Motion;
using Xunit;

namespace Pulse.Tests;

public sealed class RestingContourTests
{
    [Theory]
    [InlineData(1.25f)] [InlineData(1.5f)] [InlineData(2f)] [InlineData(3f)]
    public void PhysicalMaskAndLogicalSurfaceAgreeAtEveryDisplayScale(float scale)
    {
        foreach (var edge in new[] { "Top edge", "Bottom edge", "Left edge", "Right edge" })
        foreach (var notch in new[] { .2, .75, 1d })
        {
            var horizontal = edge is "Top edge" or "Bottom edge";
            var width = horizontal ? 124f : 48f; var height = horizontal ? 34f : 112f;
            var logical = RestingContour.BezierPoints(width, height, edge, .8, notch);
            var physical = RestingContour.BezierPoints(width * scale, height * scale, edge, .8, notch, scale);
            for (var i = 0; i < logical.Length; i++)
                Assert.InRange(Vector2.Distance(logical[i] * scale, physical[i]), 0, .001f);
        }
    }
    [Theory]
    [InlineData(.001)] [InlineData(.25)] [InlineData(.5)] [InlineData(.75)] [InlineData(1)]
    public void DropToNotchMorphHasContinuousTangentsAndStaysInsideTheWindow(double notch)
    {
        foreach (var edge in new[] { "Top edge", "Bottom edge", "Left edge", "Right edge" })
        {
            var points = RestingContour.BezierPoints(220, 38, edge, 1, notch);
            Assert.All(points, p => { Assert.InRange(p.X, -.001f, 220.001f); Assert.InRange(p.Y, -.001f, 38.001f); });
            for (var i = 3; i < points.Length - 1; i += 3)
                Assert.InRange(Vector2.Distance(points[i] - points[i - 1], points[i + 1] - points[i]), 0, .001f);
        }
    }
    [Fact]
    public void ContentNotchHasAWideFlatBodyAndMirrorsAtTheOppositeEdge()
    {
        var top = RestingContour.BezierPoints(220, 38, "Top edge", 1, 1);
        var bottom = RestingContour.BezierPoints(220, 38, "Bottom edge", 1, 1);
        for (var i = 6; i <= 12; i++) Assert.Equal(38, top[i].Y);
        Assert.True(top[12].X - top[6].X > 220 * .65);
        Assert.Equal(Vector2.Zero, top[0]); Assert.Equal(new Vector2(220, 0), top[^1]);
        var left = RestingContour.BezierPoints(220, 38, "Left edge", 1, 1);
        var right = RestingContour.BezierPoints(220, 38, "Right edge", 1, 1);
        for (var i = 0; i < top.Length; i++)
        {
            Assert.InRange(Vector2.Distance(top[i], new(bottom[i].X, 38 - bottom[i].Y)), 0, .001f);
            Assert.InRange(Vector2.Distance(left[i], new(220 - right[i].X, right[i].Y)), 0, .001f);
        }
    }
    [Theory]
    [InlineData("Top edge", 38)] [InlineData("Bottom edge", 0)]
    public void HoverEndsAtAFullPanelInsteadOfLeavingANotchCutout(string edge, float expected)
    { Assert.All(RestingContour.BezierPoints(220, 38, edge, 0, .5), p => Assert.InRange(Math.Abs(p.Y - expected), 0, .001f)); }

    [Fact]
    public void SmallContentKeepsRoomForArtworkAndLongerTimersGrowOnlyAsNeeded()
    {
        var mediaWidth = DockPresentation.LiveNotchWidthFor(60);
        var timerWidth = DockPresentation.LiveNotchWidthFor(82);
        Assert.Equal(124, mediaWidth); Assert.Equal(146, timerWidth);
        var curve = RestingContour.BezierPoints((float)mediaWidth, (float)DockPresentation.LiveNotchHeight, "Top edge", 1, 1);
        Assert.Equal(60, curve[12].X - curve[6].X);
        Assert.True(curve[3].X > 0 && curve[3].Y < curve[6].Y);
    }

    [Fact]
    public void SideActivityUsesAVerticalEnvelopeAndARotatedSymmetricContour()
    {
        var size = DockPresentation.LiveNotchSizeFor(46, 42, false);
        Assert.True(size.Height > size.Width); Assert.True(size.Width >= 46); Assert.True(size.Height >= 42);
        var top = RestingContour.BezierPoints((float)size.Height, (float)size.Width, "Top edge", 1, 1);
        var side = RestingContour.BezierPoints((float)size.Width, (float)size.Height, "Left edge", 1, 1);
        for (var i = 0; i < side.Length; i++) Assert.InRange(Vector2.Distance(side[i], new(top[i].Y, top[i].X)), 0, .001f);
    }
}
