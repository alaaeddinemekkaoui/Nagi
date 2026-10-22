using System.Numerics;
using Pulse.Core.Motion;
using Xunit;

namespace Pulse.Tests;

public sealed class NotchContourTests
{
    [Theory]
    [InlineData(132, 18)]
    [InlineData(112, 20)]
    [InlineData(140, 25)]
    [InlineData(660, 120)]
    [InlineData(1920, 70)]
    public void TeardropIsSymmetricAndMeetsTheScreenWithoutAShoulderStrip(float width, float height)
    {
        var curve = EdgeDropContour.BezierPoints(width, height, "Top edge");
        Assert.Equal(width / 2, curve[3].X);
        Assert.Equal(height, curve[3].Y);
        Assert.Equal(0, curve[0].Y);
        Assert.Equal(0, curve[^1].Y);
        Assert.Equal(curve[0].Y, curve[1].Y);
        Assert.Equal(curve[2].Y, curve[3].Y);
        Assert.Equal(curve[3].Y, curve[4].Y);
        Assert.Equal(curve[5].Y, curve[6].Y);
        // Matching derivatives at the center prevent a kink during a size change.
        Assert.InRange(Vector2.Distance(curve[3] - curve[2], curve[4] - curve[3]), 0, .001f);
        for (var i = 0; i < curve.Length; i++)
        {
            Assert.InRange(Math.Abs(curve[i].X + curve[^(i + 1)].X - width), 0, .001f);
            Assert.Equal(curve[i].Y, curve[^(i + 1)].Y);
        }
        var polygon = EdgeDropContour.Create(width, height, "Top edge");
        Assert.All(polygon, p => { Assert.InRange(p.X, 0, width); Assert.InRange(p.Y, 0, height); });
    }

    [Theory]
    [InlineData(0)] [InlineData(.25)] [InlineData(.5)] [InlineData(.75)] [InlineData(1)]
    public void MorphAndEveryDockingEdgeUseTheSameCurve(double amount)
    {
        var top = EdgeDropContour.Create(264, 48, "Top edge", amount);
        var bottom = EdgeDropContour.Create(264, 48, "Bottom edge", amount);
        var left = EdgeDropContour.Create(48, 264, "Left edge", amount);
        var right = EdgeDropContour.Create(48, 264, "Right edge", amount);
        Assert.Equal(top.Length, bottom.Length); Assert.Equal(top.Length, left.Length);
        for (var i = 0; i < top.Length; i++)
        {
            Assert.InRange(Vector2.Distance(top[i], new(bottom[i].X, 48 - bottom[i].Y)), 0, .001f);
            Assert.InRange(Vector2.Distance(top[i], new(left[i].Y, left[i].X)), 0, .001f);
            Assert.InRange(Vector2.Distance(top[i], new(right[i].Y, 48 - right[i].X)), 0, .001f);
        }
        if (amount == 0) Assert.All(EdgeDropContour.BezierPoints(264, 48, "Top edge", 0), p => Assert.Equal(48, p.Y));
    }
}
