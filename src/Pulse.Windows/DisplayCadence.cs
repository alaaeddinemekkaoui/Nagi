using System.Runtime.InteropServices;

namespace Pulse.Windows;

public static class DisplayCadence
{
    public static double ForWindow(nint hwnd)
    {
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, 2), ref info)) return 60;
        var mode = new DisplayMode { Size = (ushort)Marshal.SizeOf<DisplayMode>() };
        return EnumDisplaySettings(info.Device, -1, ref mode) && mode.Frequency is >= 24 and <= 1000 ? mode.Frequency : 60;
    }
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 220)]
    private struct DisplayMode
    {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(184)] public uint Frequency;
    }
    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string device, int mode, ref DisplayMode settings);
}
