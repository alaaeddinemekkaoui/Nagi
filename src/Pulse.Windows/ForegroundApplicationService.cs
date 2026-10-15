using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Pulse.Core;

namespace Pulse.Windows;

public sealed class ForegroundApplicationService : IForegroundApplicationService
{
    public ForegroundApplication Sample()
    {
        var hwnd = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return ForegroundApplication.Empty;
        string? path = null;
        var handle = Native.OpenProcess(0x1000, false, pid);
        if (handle != 0)
        {
            try
            {
                var buffer = new StringBuilder(32768); uint length = (uint)buffer.Capacity;
                if (Native.QueryFullProcessImageName(handle, 0, buffer, ref length)) path = buffer.ToString();
            }
            finally { Native.CloseHandle(handle); }
        }
        string name;
        try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { name = "Restricted application"; }
        var monitor = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        var fullscreen = Native.GetWindowRect(hwnd, out var rect) && Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, 2), ref monitor)
            && rect.Left <= monitor.Monitor.Left && rect.Top <= monitor.Monitor.Top && rect.Right >= monitor.Monitor.Right
            && rect.Bottom >= monitor.Monitor.Bottom && name is not "explorer" and not "ShellExperienceHost";
        return new((int)pid, name, path, fullscreen);
    }
}
