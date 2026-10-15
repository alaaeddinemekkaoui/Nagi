using System.Runtime.InteropServices;

namespace Pulse.Windows;

public readonly record struct SearchWindowFrame(int X, int Y, int Width, int Height, double Scale);
public static class SearchWindowPlacement
{
    public static void RemoveFrame(nint window)
    {
        var style = Native.GetWindowLongPtr(window, -16).ToInt64();
        Native.SetWindowLongPtr(window, -16, (nint)(style & ~0x00C40000L));
        var extended = Native.GetWindowLongPtr(window, -20).ToInt64();
        Native.SetWindowLongPtr(window, -20, (nint)((extended | 0x80) & ~0x60300L));
        Native.SetWindowPos(window, 0, 0, 0, 0, 0, 0x0037);
        var corner = 2; Native.DwmSetWindowAttribute(window, 33, ref corner, sizeof(int));
        var noBorder = -2; Native.DwmSetWindowAttribute(window, 34, ref noBorder, sizeof(int));
        var dark = 1; Native.DwmSetWindowAttribute(window, 20, ref dark, sizeof(int));
    }
    public static SearchWindowFrame Prepare(nint foreground)
    {
        var monitor = Native.MonitorFromWindow(foreground == 0 ? Native.GetForegroundWindow() : foreground, 2);
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return new(100, 100, 680, 520, 1);
        Native.GetDpiForMonitor(monitor, 0, out var dpi, out _);
        var scale = (dpi == 0 ? 96 : dpi) / 96d;
        var width = Math.Clamp((int)(680 * scale), 100, Math.Max(100, info.Work.Width - 24));
        var height = Math.Clamp((int)(520 * scale), 100, Math.Max(100, info.Work.Height - 24));
        return new(info.Work.Left + (info.Work.Width - width) / 2, info.Work.Top + Math.Max(12, (info.Work.Height - height) / 3), width, height, scale);
    }
    public static nint Foreground => Native.GetForegroundWindow();
    public static bool IsForeground(nint window) { var active = Native.GetForegroundWindow(); return active == window || GetAncestor(active, 2) == window; }
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    public static void Hide(nint window) => Native.ShowWindow(window, 0);
    public static void Show(nint window) => Native.ShowWindow(window, 5);
}
