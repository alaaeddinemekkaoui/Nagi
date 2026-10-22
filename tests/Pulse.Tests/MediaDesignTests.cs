using Pulse.Core;
using Pulse.Core.Motion;
using Xunit;

namespace Pulse.Tests;

public sealed class MediaDesignTests
{
    [Theory]
    [InlineData("", "", false, false)]
    [InlineData(null, null, false, false)]
    [InlineData(" ", "", false, false)]
    [InlineData("Track", "Artist", false, true)]
    [InlineData("", "", true, true)]
    public void EmptyWindowsSessionsShowMusicLinksButPausedTracksRemainDetected(string? title, string? artist, bool playing, bool detected)
        => Assert.Equal(detected, MediaSnapshot.IsDetected(title, artist, playing));
    [Theory]
    [InlineData("Left edge", 18, 132)]
    [InlineData("Right edge", 18, 132)]
    [InlineData("Top edge", 132, 18)]
    [InlineData("Bottom edge", 132, 18)]
    public void NotchTouchesTheEdgeAndReachesItsFullDepthAtTheCenter(string edge, float width, float height)
    {
        var points = EdgeDropContour.Create(width, height, edge);
        Assert.All(points, point => { Assert.InRange(point.X, 0, width); Assert.InRange(point.Y, 0, height); });
        var center = EdgeDropContour.BezierPoints(width, height, edge)[3];
        if (edge == "Left edge") { Assert.Equal(0, points[0].X); Assert.Equal(width, center.X); Assert.Equal(0, points[^1].X); }
        if (edge == "Right edge") { Assert.Equal(width, points[0].X); Assert.Equal(0, center.X); Assert.Equal(width, points[^1].X); }
        if (edge == "Top edge") { Assert.Equal(0, points[0].Y); Assert.Equal(height, center.Y); Assert.Equal(0, points[^1].Y); }
        if (edge == "Bottom edge") { Assert.Equal(height, points[0].Y); Assert.Equal(0, center.Y); Assert.Equal(height, points[^1].Y); }
    }
    [Fact] public void AkenoThemeSurvivesSettingsNormalization() => Assert.Equal("Akeno", (new PulseSettings { Theme = "Akeno" }).Normalize().Theme);
    [Fact] public void AudioEnvelopeNeverInventsActivityAndReleasesToSilence()
    {
        var envelope = new AudioEnvelope();
        Assert.Equal(0, envelope.Update(null, .05));
        var attack = envelope.Update(.8, .05);
        Assert.InRange(attack, .1, .8);
        var release = envelope.Update(0, .05);
        Assert.InRange(release, .001, attack);
        for (var i = 0; i < 40; i++) envelope.Update(0, .05);
        Assert.Equal(0, envelope.Value);
        Assert.Equal(0, envelope.Update(double.NaN, .05));
    }
    [Fact] public void AudioSmoothingDoesNotDependOnFrameRate()
    {
        var slow = new AudioEnvelope(); var fast = new AudioEnvelope();
        for (var i = 0; i < 20; i++) slow.Update(.7, .05);
        for (var i = 0; i < 100; i++) fast.Update(.7, .01);
        Assert.Equal(slow.Value, fast.Value, 8);
    }
    [Theory]
    [InlineData(480, 1.0, 240, 380)]
    [InlineData(720, 1.5, 360, 380)]
    [InlineData(1080, 1.25, 540, 380)]
    [InlineData(2160, 2.0, 1700, 380)]
    [InlineData(600, 2.0, 150, 336)]
    public void PreviewFitsResolutionAndRetainsCompactStrip(double height, double scale, double center, double requested)
    {
        var fitted = EdgePreviewSizing.Fit(0, height, center, scale, requested);
        var y = fitted.Above ? center + 42 * scale - fitted.Height : center - 42 * scale;
        Assert.True(y >= 0 && y + fitted.Height <= height);
        var stripY = fitted.Above ? y + fitted.Height - 84 * scale : y;
        Assert.Equal(center - 42 * scale, stripY, 8);
    }
    [Fact] public void ContourSpringDoesNotSnapWhileShapeDifferenceIsStillVisible()
    {
        var contour = new SpringValue(1, .0005) { Target = 0 };
        contour.Advance(.15, 30);
        Assert.InRange(contour.Value, .01, .1);
        Assert.False(contour.IsSettled);
        for (var i = 0; i < 120; i++) contour.Advance(1d / 120, 30);
        Assert.True(contour.IsSettled); Assert.Equal(0, contour.Value);
    }
}
