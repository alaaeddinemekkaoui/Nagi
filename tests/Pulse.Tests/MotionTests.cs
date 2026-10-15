using Pulse.Core.Motion;
using Xunit;

namespace Pulse.Tests;

public sealed class MotionTests
{
    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)] [InlineData(144)]
    public void WindowSpringConvergesWithoutOvershootAtDifferentRefreshRates(int fps)
    {
        var spring = new SpringValue(54) { Target = 700 };
        var previous = spring.Value;
        for (var i = 0; i < fps; i++)
        {
            spring.Advance(1d / fps);
            Assert.InRange(spring.Value, previous, 700);
            previous = spring.Value;
        }
        Assert.True(spring.IsSettled); Assert.Equal(700, spring.Value);
    }
    [Fact] public void RetargetingPreservesPositionAndVelocityRatherThanSnapping()
    {
        var spring = new SpringValue(54) { Target = 700 };
        spring.Advance(.08);
        var position = spring.Value; var velocity = spring.Velocity;
        spring.Target = 54;
        Assert.Equal(position, spring.Value); Assert.Equal(velocity, spring.Velocity);
        spring.Advance(.001);
        Assert.True(spring.Value > position); // Inertia is continuous through the direction change.
        spring.Advance(2);
        Assert.Equal(54, spring.Value); Assert.Equal(0, spring.Velocity);
    }
    [Fact] public void FramePacingChangesDoNotChangeTheTrajectory()
    {
        var a = new SpringValue(54) { Target = 700 }; var b = new SpringValue(54) { Target = 700 };
        a.Advance(.25);
        for (var i = 0; i < 25; i++) b.Advance(.01);
        Assert.Equal(a.Value, b.Value, 8); Assert.Equal(a.Velocity, b.Velocity, 8);
    }
    [Fact] public void SnapResetsVelocityForReducedMotionAndDisplayChanges()
    {
        var spring = new SpringValue(54) { Target = 700 }; spring.Advance(.1);
        spring.Snap(326);
        Assert.Equal(326, spring.Value); Assert.Equal(326, spring.Target); Assert.Equal(0, spring.Velocity); Assert.True(spring.IsSettled);
    }
}
