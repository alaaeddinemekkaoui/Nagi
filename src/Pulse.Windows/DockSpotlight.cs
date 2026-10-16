using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pulse.Windows;

/// <summary>A dimmed monitor with an actual input/visual hole around the dock.</summary>
public sealed class DockSpotlight : IDisposable
{
    private nint _handle;
    public nint Handle => _handle;
    public HaloFrame Bounds { get; }
    public HaloFrame Hole { get; }
    public DockSpotlight(HaloFrame dock)
    {
        var target = new Native.Rect { Left = (int)dock.X, Top = (int)dock.Y, Right = (int)(dock.X + dock.Width), Bottom = (int)(dock.Y + dock.Height) };
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromRect(ref target, 2), ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var monitor = info.Monitor;
        Bounds = new(monitor.Left, monitor.Top, monitor.Width, monitor.Height, 0, dock.Scale);
        var margin = Math.Round(14 * dock.Scale);
        Hole = new(dock.X - monitor.Left - margin, dock.Y - monitor.Top - margin, dock.Width + margin * 2, dock.Height + margin * 2, 0, dock.Scale);
        _handle = Native.CreateWindowEx(0x08080088, "STATIC", "Nagi dock spotlight", 0x80000004,
            monitor.Left, monitor.Top, monitor.Width, monitor.Height, 0, 0, 0, 0);
        if (_handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!Native.SetLayeredWindowAttributes(_handle, 0, 155, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var region = Native.CreateRectRgn(0, 0, monitor.Width, monitor.Height);
            var hole = Native.CreateRoundRectRgn((int)Hole.X, (int)Hole.Y, (int)(Hole.X + Hole.Width), (int)(Hole.Y + Hole.Height), (int)(16 * dock.Scale), (int)(16 * dock.Scale));
            try
            {
                if (region == 0 || hole == 0 || Native.CombineRgn(region, region, hole, 4) == 0) throw new Win32Exception();
                if (Native.SetWindowRgn(_handle, region, false) == 0) throw new Win32Exception();
                region = 0; // Ownership transferred to Windows.
            }
            finally { if (region != 0) Native.DeleteObject(region); if (hole != 0) Native.DeleteObject(hole); }
        }
        catch { Dispose(); throw; }
    }
    public void Show() { if (_handle != 0) Native.ShowWindow(_handle, 4); }
    public void Dispose() { if (_handle != 0) { Native.DestroyWindow(_handle); _handle = 0; } }
}
