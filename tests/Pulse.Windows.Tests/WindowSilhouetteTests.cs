using System.Runtime.InteropServices;
using Pulse.Core;
using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class WindowSilhouetteTests
{
    [Fact] public void InterruptedMotionRetainsTheActualCornerRadius()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi motion radius test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var frame = manager.PrepareLayout(false, false, new(Anchor: "Top edge", Orientation: "Horizontal"));
            manager.ApplyFrame(frame with { Radius = 8 });
            Assert.Equal(8, manager.CurrentFrame.Radius);
            manager.ApplyFrame(manager.CurrentFrame with { Radius = 19 });
            Assert.Equal(19, manager.CurrentFrame.Radius);
        }
        finally { DestroyWindow(hwnd); }
    }
    [Theory][InlineData("Left edge")][InlineData("Right edge")][InlineData("Top edge")][InlineData("Bottom edge")]
    public void RoundedWindowActuallyExcludesTheOpaqueCornerPixels(string edge)
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi silhouette test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, hwnd);
        var region = CreateRectRgn(0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var position = new HaloPosition(Anchor: edge, Orientation: edge is "Top edge" or "Bottom edge" ? "Horizontal" : "Vertical");
            manager.ApplyFrame(manager.PrepareLayout(false, false, position, 84, true));
            var rail = manager.PrepareLayout(false, false, position, 84);
            manager.ApplyFrame(rail);
            Assert.NotEqual(0, GetWindowRgn(hwnd, region));
            Assert.Equal(edge is "Left edge" or "Top edge", PtInRegion(region, 0, 0));
            Assert.Equal(edge is "Right edge" or "Top edge", PtInRegion(region, (int)rail.Width - 1, 0));
            Assert.Equal(edge is "Left edge" or "Bottom edge", PtInRegion(region, 0, (int)rail.Height - 1));
            Assert.Equal(edge is "Right edge" or "Bottom edge", PtInRegion(region, (int)rail.Width - 1, (int)rail.Height - 1));
            Assert.True(PtInRegion(region, (int)rail.Width / 2, (int)rail.Height / 2));
            Assert.True(PtInRegion(region, 1, (int)rail.Height / 2));
        }
        finally { DeleteObject(region); DestroyWindow(hwnd); }
    }
    [Fact] public void ControlCenterFitsAndCentersInsideTheMonitorWorkAreaThenReturnsToDock()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi center test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var position = new HaloPosition(Anchor: "Top edge", Orientation: "Horizontal");
            var frame = manager.PrepareControlCenter(position); manager.ApplyFrame(frame);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info));
            Assert.InRange(frame.X, info.Work.Left + 8, info.Work.Right - frame.Width);
            Assert.InRange(frame.Y, info.Work.Top + 8, info.Work.Bottom - frame.Height);
            Assert.InRange(Math.Abs(frame.X + frame.Width / 2 - (info.Work.Left + info.Work.Right) / 2d), 0, 1);
            Assert.InRange(Math.Abs(frame.Y + frame.Height / 2 - (info.Work.Top + info.Work.Bottom) / 2d), 0, 1);
            var drop = manager.PrepareLayout(false, false, position, 84, true);
            Assert.Equal(1, drop.DropAmount); Assert.Equal(info.Work.Top, drop.Y);
        }
        finally { DestroyWindow(hwnd); }
    }
    [Fact] public void SettingsResizeHasNativeGripsKeepsItsSizeAndReturnsToANonResizableDock()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi resize test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var position = new HaloPosition();
            var frame = manager.PrepareControlCenter(position); manager.ApplyFrame(frame);
            Assert.Equal(0x00CF0000L, GetWindowLongPtr(hwnd, -16).ToInt64() & 0x00CF0000L);
            Assert.True(GetClientRect(hwnd, out var client));
            Assert.Equal(frame.Height, client.Bottom); // WinUI reserves its title strip inside the client area.
            var point = (nint)(((long)(int)(frame.Y + frame.Height / 2) << 16) | ((long)(int)(frame.X + frame.Width - 2) & 0xffff));
            Assert.Equal((nint)11, SendMessage(hwnd, 0x84, 0, point));
            var resized = frame with { Width = frame.Width - 60, Height = frame.Height - 40 };
            manager.ApplyFrame(resized);
            var retained = manager.PrepareControlCenter(position);
            Assert.Equal(resized.Width, retained.Width); Assert.Equal(resized.Height, retained.Height);
            manager.PrepareLayout(false, false, position, 84, true);
            Assert.Equal(0, GetWindowLongPtr(hwnd, -16).ToInt64() & 0x00CF0000L);
        }
        finally { DestroyWindow(hwnd); }
    }
    [Fact] public void CaptionCloseRequestsReturnToDockInsteadOfTerminatingTheApplication()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi close test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            manager.ApplyFrame(manager.PrepareControlCenter(new()));
            var closeRequested = false;
            manager.EscapeRequested += () => closeRequested = true;
            SendMessage(hwnd, 0x0112, 0xF060, 0);
            Assert.True(closeRequested);
            Assert.NotEqual(0, GetWindowLongPtr(hwnd, -16));
        }
        finally { DestroyWindow(hwnd); }
    }
    [Fact] public void FirstRunSpotlightLeavesTheDockUncoveredAndDestroysItsWindow()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi spotlight target test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        var region = CreateRectRgn(0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var dock = manager.PrepareLayout(false, false, new(Anchor: "Top edge", Orientation: "Horizontal"), 84, true);
            using var spotlight = new DockSpotlight(dock);
            Assert.NotEqual(0, spotlight.Handle); Assert.False(IsWindowVisible(spotlight.Handle));
            Assert.NotEqual(0, GetWindowRgn(spotlight.Handle, region));
            Assert.False(PtInRegion(region, (int)(dock.X - spotlight.Bounds.X + dock.Width / 2), (int)(dock.Y - spotlight.Bounds.Y + dock.Height / 2)));
            Assert.True(PtInRegion(region, (int)spotlight.Bounds.Width / 2, (int)spotlight.Bounds.Height / 2));
            spotlight.Dispose(); Assert.Equal(0, spotlight.Handle);
        }
        finally { DeleteObject(region); DestroyWindow(hwnd); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint hwnd, nint region);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
