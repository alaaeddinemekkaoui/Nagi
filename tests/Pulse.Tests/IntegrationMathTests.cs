using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class IntegrationMathTests
{
    [Fact] public void GpuTotalsUseTheBusiestPhysicalEngineWithoutDoubleCountingEngines()
    {
        Assert.Equal(70, IntegrationMath.BusiestGpuEngine([("gpu0-3d", 40), ("gpu0-3d", 30), ("gpu0-copy", 20), ("gpu1-3d", 50)]));
        Assert.Equal(100, IntegrationMath.BusiestGpuEngine([("one", 80), ("one", 80)]));
        Assert.Null(IntegrationMath.BusiestGpuEngine([("bad", double.NaN), ("bad", -1)]));
    }
    [Fact] public void ObsAuthenticationMatchesAnIndependentProtocolVector()
        => Assert.Equal("39cfhx7et2iyoMZvoQ6o3OPLNSKgtMmy48GQ7jnvsdE=", IntegrationMath.ObsAuthentication("secret", "salt", "challenge"));
    [Fact] public void FpsUsesTheTargetProcessAndDominantSwapChain()
    {
        var fps = IntegrationMath.FpsFromCsv(["Application,ProcessID,SwapChainAddress,MsBetweenPresents", "game,12,A,20", "game,12,A,20", "game,12,B,100", "other,13,A,1"], 12);
        Assert.Equal(50, fps);
    }
    [Fact] public void FpsParserAcceptsQuotedNamesAndInvariantDecimalValues()
        => Assert.Equal(100, IntegrationMath.FpsFromCsv(["Application,ProcessID,MsBetweenPresents", "\"game, name\",5,10.0", "game,5,10.0"], 5));
    [Fact] public void FpsDoesNotInventAReadingForMissingOrInvalidFrames()
    {
        Assert.Null(IntegrationMath.FpsFromCsv([], 5));
        Assert.Null(IntegrationMath.FpsFromCsv(["ProcessID,MsBetweenPresents", "5,NaN", "5,0", "5,-1", "5,Infinity"], 5));
        Assert.Null(IntegrationMath.FpsFromCsv(["ProcessID,MsBetweenPresents", "5,10"], 5));
        Assert.Null(IntegrationMath.FpsFromCsv(["ProcessID,Other", "5,10", "5,10"], 5));
    }
    [Fact] public void ProviderPreferencesDefaultToNoTraceOrObsAndNormalizePorts()
    {
        var defaults = new PulseSettings(); Assert.False(defaults.FpsEnabled); Assert.False(defaults.ObsEnabled); Assert.Equal(4455, defaults.ObsPort);
        Assert.Equal(1024, new PulseSettings { ObsPort = -5 }.Normalize().ObsPort);
        Assert.Equal(65535, new PulseSettings { ObsPort = 70000 }.Normalize().ObsPort);
    }
}
