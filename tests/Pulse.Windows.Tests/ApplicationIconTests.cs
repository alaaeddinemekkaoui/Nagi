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

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public async Task NativeIconEdgesRemainTransparentAndPremultipliedAtDisplaySizes(int size)
    {
        var path = Path.Combine(Path.GetTempPath(), "Nagi-icon-" + Guid.NewGuid().ToString("N") + ".ico");
        try
        {
            // A real shell-decoded ICO with a transparent surround and half-alpha red.
            // Checking the actual color also detects accidental double premultiplication.
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                const int side = 64, maskBytes = side * side / 8;
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
                writer.Write((byte)side); writer.Write((byte)side); writer.Write((ushort)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(40 + side * side * 4 + maskBytes); writer.Write(22);
                writer.Write(40); writer.Write(side); writer.Write(side * 2);
                writer.Write((ushort)1); writer.Write((ushort)32);
                for (var i = 0; i < 6; i++) writer.Write(0);
                for (var y = 0; y < side; y++)
                    for (var x = 0; x < side; x++)
                    {
                        var inside = x >= 8 && x < 56 && y >= 8 && y < 56;
                        writer.Write((byte)0); writer.Write((byte)0);
                        writer.Write((byte)(inside ? 240 : 0)); writer.Write((byte)(inside ? 128 : 0));
                    }
                writer.Write(new byte[maskBytes]);
            }
            var icon = await ApplicationIcons.ReadAsync(path, size);
            Assert.NotNull(icon);
            Assert.Contains(Enumerable.Range(0, icon.Width * icon.Height), pixel => icon.Bgra[pixel * 4 + 3] == 0);
            var center = (icon.Height / 2 * icon.Width + icon.Width / 2) * 4;
            Assert.InRange(icon.Bgra[center + 2], 118, 122);
            Assert.InRange(icon.Bgra[center + 3], 127, 129);
            for (var i = 0; i < icon.Bgra.Length; i += 4)
                for (var channel = 0; channel < 3; channel++)
                    Assert.True(icon.Bgra[i + channel] <= icon.Bgra[i + 3], "Edge colors must not exceed their alpha coverage.");
        }
        finally { File.Delete(path); }
    }
}
