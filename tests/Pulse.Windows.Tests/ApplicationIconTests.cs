using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class ApplicationIconTests
{
    [Fact]
    public async Task InstalledExecutableHasUsableNativePixelsAndMissingAppsFailGracefully()
    {
        var icon = await ApplicationIcons.ReadAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"));
        Assert.NotNull(icon);
        Assert.InRange(icon.Width, 1, 256);
        Assert.Equal(icon.Width * icon.Height * 4, icon.Bgra.Length);
        Assert.Contains(icon.Bgra, pixel => pixel != 0);
        Assert.Null(await ApplicationIcons.ReadAsync("Nagi.MissingApp." + Guid.NewGuid().ToString("N")));
    }
}
