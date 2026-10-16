using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class LaunchOptionsTests
{
    [Fact] public void NormalLaunchRetainsTheExistingSingleInstanceIdentity()
    {
        var options = NagiLaunchOptions.Parse([]);
        Assert.False(options.ShowSetup); Assert.Null(options.DataDirectory); Assert.Null(options.WaitForExit);
        Assert.Equal("Local\\Pulse.Desktop.Activate.v1", options.Scope("Local\\Pulse.Desktop.Activate.v1"));
    }
    [Fact] public void SetupReplayAndRestartKeepTheExplicitProfile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Nagi isolated test");
        var options = NagiLaunchOptions.Parse(["--setup", "--wait-for-exit", "123", "--data-dir", directory]);
        Assert.True(options.ShowSetup); Assert.Equal(123, options.WaitForExit); Assert.Equal(directory, options.DataDirectory);
        var second = NagiLaunchOptions.Parse(["--data-dir", directory]);
        Assert.Equal(options.Scope("activation"), second.Scope("activation"));
        Assert.NotEqual("activation", options.Scope("activation"));
        Assert.NotEqual(options.Scope("activation"), NagiLaunchOptions.Parse(["--data-dir", directory + "2"]).Scope("activation"));
    }
    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--wait-for-exit")]
    [InlineData("--unknown")]
    public void IncompleteOrUnknownOptionsAreRejected(string value) => Assert.Throws<ArgumentException>(() => NagiLaunchOptions.Parse([value]));
    [Fact] public void RelativePathsAndInvalidProcessIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => NagiLaunchOptions.Parse(["--data-dir", "relative"]));
        Assert.Throws<ArgumentException>(() => NagiLaunchOptions.Parse(["--wait-for-exit", "0"]));
        Assert.Throws<ArgumentException>(() => NagiLaunchOptions.Parse(["--wait-for-exit", "other"]));
    }
}
