using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Pulse.Windows;

public sealed record ApplicationIconPixels(int Width, int Height, byte[] Bgra);

public static class ApplicationIcons
{
    private static readonly ConcurrentDictionary<string, Task<ApplicationIconPixels?>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim Worker = new(1, 1);

    public static Task<ApplicationIconPixels?> ReadAsync(string target, int pixelSize = 64)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > 2048 || target.StartsWith("\\\\", StringComparison.Ordinal)) return Task.FromResult<ApplicationIconPixels?>(null);
        if (Cache.Count >= 256)
        {
            foreach (var key in Cache.Where(p => p.Value.IsCompleted).Take(64).Select(p => p.Key)) Cache.TryRemove(key, out _);
            if (Cache.Count >= 256) return Task.FromResult<ApplicationIconPixels?>(null);
        }
        // Keep an original large shell image; let XAML filter it at the actual display scale.
        // Asking the shell to shrink to 32px uses a lower-quality GDI stretch.
        var size = pixelSize <= 64 ? 64 : pixelSize <= 128 ? 128 : 256;
        return Cache.GetOrAdd(target + "\0" + size, _ => LoadAsync(target, size));
    }
    private static async Task<ApplicationIconPixels?> LoadAsync(string target, int pixelSize)
    {
        await Worker.WaitAsync().ConfigureAwait(false);
        try
        {
            var completion = new TaskCompletionSource<ApplicationIconPixels?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { completion.TrySetResult(Read(target, pixelSize)); }
                catch (Exception ex) when (ex is COMException or ArgumentException or UnauthorizedAccessException)
                { completion.TrySetResult(null); }
            }) { IsBackground = true, Name = "Nagi native app icon" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            return await completion.Task.ConfigureAwait(false);
        }
        finally { Worker.Release(); }
    }
    private static ApplicationIconPixels? Read(string target, int pixelSize)
    {
        var name = Path.IsPathFullyQualified(target) || target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            ? target : "shell:AppsFolder\\" + target;
        var iid = typeof(IShellItemImageFactory).GUID;
        IShellItemImageFactory? factory = null; nint bitmap = 0, dc = 0;
        try
        {
            if (SHCreateItemFromParsingName(name, 0, ref iid, out factory) < 0 || factory is null) return null;
            const uint iconOnlyAndBiggerSizeOk = 4 | 1;
            if (factory.GetImage(new Size(pixelSize, pixelSize), iconOnlyAndBiggerSizeOk, out bitmap) < 0 || bitmap == 0) return null;
            if (GetObject(bitmap, Marshal.SizeOf<Bitmap>(), out var dimensions) == 0) return null;
            var width = dimensions.Width; var height = Math.Abs(dimensions.Height);
            if (width is <= 0 or > 256 || height is <= 0 or > 256) return null;
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            var pixels = new byte[checked(width * height * 4)];
            dc = GetDC(0);
            if (GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref info, 0) == 0) return null;
            if (!Enumerable.Range(0, width * height).Any(p => pixels[p * 4 + 3] != 0))
                for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            // Shell DIB colors use straight alpha. WriteableBitmap expects premultiplied
            // BGRA: copying straight colors makes transparent edge pixels bright/square.
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                pixels[i] = (byte)((pixels[i] * alpha + 127) / 255);
                pixels[i + 1] = (byte)((pixels[i + 1] * alpha + 127) / 255);
                pixels[i + 2] = (byte)((pixels[i + 2] * alpha + 127) / 255);
            }
            return new(width, height, pixels);
        }
        finally
        {
            if (dc != 0) ReleaseDC(0, dc);
            if (bitmap != 0) DeleteObject(bitmap);
            if (factory is not null && Marshal.IsComObject(factory)) Marshal.ReleaseComObject(factory);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private readonly struct Size(int width, int height) { public readonly int Width = width, Height = height; }
    [StructLayout(LayoutKind.Sequential)] private struct Bitmap { public int Type, Width, Height, WidthBytes; public short Planes, BitsPixel; public nint Bits; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPels, YPels; public uint ColorsUsed, ColorsImportant; public uint Color; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [PreserveSig] int GetImage(Size size, uint flags, out nint bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? item);
    [DllImport("gdi32.dll")] private static extern int GetObject(nint value, int size, out Bitmap bitmap);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] pixels, ref BitmapInfo info, uint usage);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
}
