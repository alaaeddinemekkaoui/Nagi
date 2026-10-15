using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class PowerSnapshotTests
{
    [Theory]
    [InlineData(0, 2, 20, 20, true, false, false, "20%")]
    [InlineData(1, 8, 87, 87, true, true, true, "87%")]
    [InlineData(1, 1, 100, 100, true, false, true, "100%")]
    [InlineData(0, 4, 0, 0, true, false, false, "0%")]
    [InlineData(1, 128, 255, null, false, null, true, "AC")]
    [InlineData(1, 128, 100, null, false, null, true, "AC")]
    [InlineData(255, 255, 255, null, null, null, null, "—")]
    [InlineData(1, 255, 42, 42, null, null, true, "42%")]
    [InlineData(0, 0, 200, null, true, false, false, "—")]
    public void DecodesChargeAndUnknownSentinelsWithoutInventingPercent(byte ac, byte flags, byte rawPercent,
        int? percent, bool? present, bool? charging, bool? pluggedIn, string text)
    {
        var power = PowerSnapshot.FromWindows(ac, flags, rawPercent);
        Assert.Equal(percent, power.Percent); Assert.Equal(present, power.HasBattery);
        Assert.Equal(charging, power.Charging); Assert.Equal(pluggedIn, power.PluggedIn);
        Assert.Equal(text, power.CompactText);
    }

    [Fact] public void DetailDistinguishesChargingFromPluggedInAndUnavailable()
    {
        Assert.Contains("Charging", PowerSnapshot.FromWindows(1, 8, 70).Detail);
        Assert.Contains("Plugged in", PowerSnapshot.FromWindows(1, 1, 100).Detail);
        Assert.Contains("On battery", PowerSnapshot.FromWindows(0, 2, 20).Detail);
        Assert.Contains("unavailable", PowerSnapshot.Unknown.Detail);
        Assert.Contains("No system battery", PowerSnapshot.FromWindows(1, 128, 255).Detail);
    }
}
