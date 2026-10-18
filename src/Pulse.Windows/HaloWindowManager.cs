using System.ComponentModel;
using System.Runtime.InteropServices;
using Pulse.Core;

namespace Pulse.Windows;

public sealed record DisplayMonitor(string Device, string Label);
public readonly record struct HaloFrame(double X, double Y, double Width, double Height, double Radius, double Scale = 1, double DropAmount = 0);
public sealed class HaloWindowManager : IWindowManager
{
    private readonly nint _hwnd;
    private readonly Native.SubclassProc _callback;
    private HaloPosition _position = new();
    private bool _expanded, _dashboard, _disposed, _layout, _dragging;
    private int _hotkeyId, _hideHotkeyId, _searchHotkeyId;
    private bool? _visible;
    private double _dropAmount, _radius;
    private bool _hasFrame;
    private bool _controlCenter;
    public event Action<HaloFrame>? FrameApplied;
    public event Action<HaloFrame>? ControlCenterResized;
    public bool PreviewAbove { get; private set; }
    public event Action? ToggleRequested;
    public event Action? HideRequested;
    public event Action? SearchRequested;
    public event Action? DisplayChanged;
    public event Action<HaloPosition>? PositionChanged;
    public event Action? EscapeRequested;

    public HaloWindowManager(nint hwnd)
    {
        _hwnd = hwnd; _callback = WindowProcedure;
        if (!Native.SetWindowSubclass(hwnd, _callback, 1, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var style = Native.GetWindowLongPtr(hwnd, -20).ToInt64();
        Native.SetWindowLongPtr(hwnd, -20, (nint)((style | 0x80) & ~0x60300L)); // Tool window, never click-through.
        // WinUI leaves WS_DLGFRAME behind even when the presenter hides its border.
        // Remove the native frame as well, so the client content meets the screen exactly.
        var windowStyle = Native.GetWindowLongPtr(hwnd, -16).ToInt64();
        Native.SetWindowLongPtr(hwnd, -16, (nint)(windowStyle & ~0x00C40000L));
        Native.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0037); // FRAMECHANGED, NOMOVE/NOSIZE/NOZORDER/NOACTIVATE.
        // The HWND region owns rounding, including the nonrectangular resting drop.
        // Forced DWM rounding leaves a rounded glass plate behind that region.
        var corners = 1; Native.DwmSetWindowAttribute(hwnd, 33, ref corners, sizeof(int));
        var dark = 1; Native.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        var noSystemBorder = -2; Native.DwmSetWindowAttribute(hwnd, 34, ref noSystemBorder, sizeof(int));
    }
    private static List<(nint Handle, Native.MonitorInfo Info)> Monitors()
    {
        var monitors = new List<(nint, Native.MonitorInfo)>();
        Native.EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
            if (Native.GetMonitorInfo(monitor, ref info)) monitors.Add((monitor, info));
            return true;
        }, 0);
        return monitors;
    }
    public void SuppressSystemCorners()
    {
        SetControlCenterMode(_controlCenter);
        var corners = 1; Native.DwmSetWindowAttribute(_hwnd, 33, ref corners, sizeof(int));
        var nonClient = _controlCenter ? 0 : 1;
        Native.DwmSetWindowAttribute(_hwnd, 2, ref nonClient, sizeof(int));
    }
    public static IReadOnlyList<DisplayMonitor> GetMonitors() => Monitors().Select((m, index) =>
        new DisplayMonitor(m.Info.Device, "Display " + (index + 1) + ((m.Info.Flags & 1) != 0 ? " · primary" : ""))).ToArray();

    public void SetLayout(bool expanded, bool dashboard, HaloPosition position)
    {
        ApplyFrame(PrepareLayout(expanded, dashboard, position));
    }
    public HaloFrame CurrentFrame
    {
        get
        {
            Native.GetWindowRect(_hwnd, out var rect);
            var scale = Math.Max(96, Native.GetDpiForWindow(_hwnd)) / 96d;
            return new(rect.Left, rect.Top, rect.Width, rect.Height, _hasFrame ? _radius : 26 * scale, scale, _dropAmount);
        }
    }
    public bool OwnsForeground
    {
        get { Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out var pid); return pid == Environment.ProcessId; }
    }
    public bool IsVisible => Native.IsWindowVisible(_hwnd);
    public bool TryGetPointerScreenPosition(out double x, out double y)
    {
        var available = Native.GetCursorPos(out var point);
        x = point.X; y = point.Y;
        return available;
    }
    public bool TryGetPointerPosition(out double x, out double y)
    {
        x = y = 0;
        if (!Native.GetCursorPos(out var point) || !Native.ScreenToClient(_hwnd, ref point)) return false;
        var scale = Math.Max(96, Native.GetDpiForWindow(_hwnd)) / 96d;
        x = point.X / scale; y = point.Y / scale;
        return true;
    }
    public HaloFrame PrepareLayout(bool expanded, bool dashboard, HaloPosition position, double compactHeight = 54, bool edgeResting = false, double railLength = 0, double railThickness = 0)
    {
            SetControlCenterMode(false);
            _expanded = expanded; _dashboard = dashboard; _position = position;
            var monitors = Monitors();
            if (monitors.Count == 0) return CurrentFrame;
            var selected = monitors.FirstOrDefault(m => m.Info.Device == position.Monitor);
            if (selected.Handle == 0) selected = monitors.FirstOrDefault(m => (m.Info.Flags & 1) != 0);
            if (selected.Handle == 0) selected = monitors[0];
            Native.GetDpiForMonitor(selected.Handle, 0, out var dpi, out _);
            var scale = (dpi == 0 ? 96 : dpi) / 96d;
            var work = selected.Info.Work;
            if (position.Orientation == "Horizontal")
            {
                var preview = !expanded && !edgeResting && compactHeight > 84;
                var railWidth = Math.Min(work.Width - 16, Math.Round((expanded ? 760 : railLength > 0 ? railLength : 760) * scale));
                var widthH = edgeResting && !expanded ? Math.Min(railWidth, Math.Round(132 * scale)) : railWidth;
                var heightH = Math.Min(work.Height - 16, Math.Round((expanded ? dashboard ? 800 : 780 : edgeResting ? 18 : preview ? 400 : railThickness > 0 ? railThickness : 76) * scale));
                var centerX = Math.Clamp(work.Left + work.Width / 2d + position.OffsetX * scale,
                    work.Left + railWidth / 2 + 8, Math.Max(work.Left + railWidth / 2 + 8, work.Right - railWidth / 2 - 8));
                var top = position.Anchor is "Top edge" or "Top center";
                PreviewAbove = !top;
                return new(Math.Round(centerX - widthH / 2), top ? work.Top : work.Bottom - heightH,
                    widthH, heightH, 22 * scale, scale, edgeResting && !expanded ? 1 : 0);
            }
            var edge = position.Anchor is "Left edge" or "Right edge";
            if (edge)
            {
                var screen = selected.Info.Monitor;
                var preview = !expanded && !edgeResting && compactHeight > 84;
                var edgeWidth = Math.Min(work.Width - 16, (int)Math.Round((expanded ? dashboard ? 720 : 580 : edgeResting ? 18 : preview ? 430 : railThickness > 0 ? railThickness : 80) * scale));
                var railHeight = Math.Min((railLength > 0 ? railLength : 560) * scale, work.Height - 16);
                var center = Math.Clamp(work.Top + work.Height / 2d + position.OffsetY * scale, work.Top + railHeight / 2 + 8, Math.Max(work.Top + railHeight / 2 + 8, work.Bottom - railHeight / 2 - 8));
                var edgeHeight = Math.Min(work.Height - 16, (int)Math.Round(expanded ? (dashboard ? 800 : 780) * scale : edgeResting ? 132 * scale : preview ? Math.Max(railHeight, compactHeight * scale) : railHeight));
                PreviewAbove = false;
                var edgeY = Math.Clamp(center - edgeHeight / 2d, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - edgeHeight - 8));
                return new(position.Anchor == "Left edge" ? screen.Left : screen.Right - edgeWidth, Math.Round(edgeY), edgeWidth, edgeHeight,
                    22 * scale, scale, edgeResting && !expanded ? 1 : 0);
            }
            var width = Math.Min(work.Width - 16, (int)Math.Round((expanded ? (dashboard ? 680 : 500) : 326) * scale));
            var height = Math.Min(work.Height - 16, (int)Math.Round((expanded ? (dashboard ? 760 : 740) : Math.Clamp(compactHeight, 54, 306)) * scale));
            var x = work.Left + (work.Width - width) / 2;
            var y = work.Bottom - height - (int)(16 * scale);
            if (position.Anchor == "Top center") y = work.Top + (int)(16 * scale);
            if (position.Anchor == "Bottom right") x = work.Right - width - (int)(16 * scale);
            if (position.Anchor == "Bottom left") x = work.Left + (int)(16 * scale);
            x = Math.Clamp(x + (int)(position.OffsetX * scale), work.Left + 8, Math.Max(work.Left + 8, work.Right - width - 8));
            y = Math.Clamp(y + (int)(position.OffsetY * scale), work.Top + 8, Math.Max(work.Top + 8, work.Bottom - height - 8));
            return new(x, y, width, height, (expanded ? 28 : 27) * scale, scale);
    }
    public void ApplyFrame(HaloFrame frame)
    {
        if (_disposed || _layout) return;
        _layout = true;
        try
        {
            var width = Math.Max(16, (int)Math.Round(frame.Width));
            var height = Math.Max(16, (int)Math.Round(frame.Height));
            var x = (int)Math.Round(frame.X); var y = (int)Math.Round(frame.Y);
            if (!_hasFrame || !Native.GetWindowRect(_hwnd, out var current) || current.Left != x || current.Top != y || current.Width != width || current.Height != height)
                Native.SetWindowPos(_hwnd, _controlCenter ? -2 : -1, x, y, width, height, 0x0010);
            _dropAmount = Math.Clamp(frame.DropAmount, 0, 1);
            _radius = frame.Radius;
            if (_dropAmount > .001)
            {
                var contour = Pulse.Core.Motion.EdgeDropContour.Create(width, height, _position.Anchor, _dropAmount);
                var points = contour.Select(p => new Native.Point { X = (int)Math.Round(p.X), Y = (int)Math.Round(p.Y) }).ToArray();
                var region = Native.CreatePolygonRgn(points, points.Length, 1);
                if (region != 0 && Native.SetWindowRgn(_hwnd, region, true) == 0) Native.DeleteObject(region);
            }
            else
            {
                // Borderless WinUI hosts do not reliably receive DWM corner clipping.
                // Clip the HWND itself so the backdrop can never paint black outside the silhouette.
                var diameter = (int)Math.Round(Math.Min(frame.Radius * 2, Math.Min(frame.Width, frame.Height)));
                var region = Native.CreateRoundRectRgn(0, 0, (int)Math.Round(frame.Width) + 1, (int)Math.Round(frame.Height) + 1, diameter, diameter);
                if (region != 0 && !_controlCenter)
                {
                    // The attached edge stays flat against the display; only the inward corners round.
                    var radius = Math.Max(1, diameter / 2);
                    var flat = _position.Anchor switch
                    {
                        "Left edge" => Native.CreateRectRgn(0, 0, radius, height),
                        "Right edge" => Native.CreateRectRgn(width - radius, 0, width, height),
                        "Top edge" => Native.CreateRectRgn(0, 0, width, radius),
                        "Bottom edge" => Native.CreateRectRgn(0, height - radius, width, height),
                        _ => 0
                    };
                    if (flat != 0) { Native.CombineRgn(region, region, flat, 2); Native.DeleteObject(flat); }
                }
                if (region != 0 && Native.SetWindowRgn(_hwnd, region, true) == 0) Native.DeleteObject(region);
            }
            _hasFrame = true;
            FrameApplied?.Invoke(frame);

        }
        finally { _layout = false; }
    }
    public HaloFrame PrepareControlCenter(HaloPosition position)
    {
        var alreadyOpen = _controlCenter;
        SetControlCenterMode(true);
        _expanded = _dashboard = true; _position = position;
        if (alreadyOpen && _hasFrame) return CurrentFrame with { DropAmount = 0 };
        var monitors = Monitors();
        if (monitors.Count == 0) return CurrentFrame;
        var selected = monitors.FirstOrDefault(m => m.Info.Device == position.Monitor);
        if (selected.Handle == 0) selected = monitors.FirstOrDefault(m => (m.Info.Flags & 1) != 0);
        if (selected.Handle == 0) selected = monitors[0];
        Native.GetDpiForMonitor(selected.Handle, 0, out var dpi, out _);
        var scale = (dpi == 0 ? 96 : dpi) / 96d;
        var work = selected.Info.Work;
        var width = Math.Min(work.Width - 32, Math.Round(960 * scale));
        var height = Math.Min(work.Height - 32, Math.Round(760 * scale));
        return new(Math.Round(work.Left + (work.Width - width) / 2), Math.Round(work.Top + (work.Height - height) / 2), width, height, 22 * scale, scale);
    }
    private void SetControlCenterMode(bool enabled)
    {
        var style = Native.GetWindowLongPtr(_hwnd, -16).ToInt64();
        // WinUI presenter changes can restore a native caption even when its title bar is hidden.
        // A tiny dock would then show only that caption instead of its client content.
        var desired = style & ~0x00C40000L;
        if (enabled) desired |= 0x00040000L;
        var extended = Native.GetWindowLongPtr(_hwnd, -20).ToInt64();
        var desiredExtended = enabled ? (extended | 0x40000L) & ~0x20380L : (extended | 0x80L) & ~0x60300L;
        if (_controlCenter == enabled && style == desired && extended == desiredExtended) return;
        _controlCenter = enabled;
        Native.SetWindowLongPtr(_hwnd, -16, (nint)desired);
        Native.SetWindowLongPtr(_hwnd, -20, (nint)desiredExtended);
        var prior = _layout; _layout = true;
        try { Native.SetWindowPos(_hwnd, enabled ? -2 : -1, 0, 0, 0, 0, 0x0033); }
        finally { _layout = prior; }
    }
    public void SetVisible(bool visible)
    {
        if (_visible == visible && Native.IsWindowVisible(_hwnd) == visible) return;
        _visible = visible;
        Native.ShowWindow(_hwnd, visible ? 4 : 0);
    }
    public void Drag()
    {
        Native.ReleaseCapture();
        Native.SendMessage(_hwnd, 0x00A1, 2, 0);
    }
    public void MoveDockDrag(HaloFrame frame)
    {
        _dragging = true;
        ApplyFrame(frame);
    }
    public bool IsPrimaryButtonPressed => (Native.GetAsyncKeyState(1) & 0x8000) != 0;
    public void CancelDockDrag() => _dragging = false;
    public void FinishDockDrag()
    {
        _dragging = false;
        if (!Native.GetWindowRect(_hwnd, out var rect)) return;
        var monitor = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(_hwnd, 2), ref monitor)) return;
        var scale = Native.GetDpiForWindow(_hwnd) / 96d;
        if (_position.Orientation == "Horizontal")
        {
            var top = rect.Top + rect.Height / 2d < monitor.Work.Top + monitor.Work.Height / 2d;
            _position = _position with { Monitor = monitor.Device, Anchor = top ? "Top edge" : "Bottom edge", OffsetY = 0,
                OffsetX = (rect.Left + rect.Width / 2d - (monitor.Work.Left + monitor.Work.Width / 2d)) / scale };
            PositionChanged?.Invoke(_position); return;
        }
        var anchor = rect.Left + rect.Width / 2d < (monitor.Monitor.Left + monitor.Monitor.Right) / 2d ? "Left edge" : "Right edge";
        _position = _position with { Monitor = monitor.Device, Anchor = anchor, OffsetX = 0,
            OffsetY = (rect.Top + rect.Height / 2d - (monitor.Work.Top + monitor.Work.Height / 2d)) / scale };
        PositionChanged?.Invoke(_position);
    }
    public OperationResult RegisterHotkey(Hotkey hotkey)
    {
        return RegisterShortcut(hotkey, ref _hotkeyId, 0x5055);
    }
    public OperationResult RegisterHideHotkey(Hotkey hotkey)
    {
        return RegisterShortcut(hotkey, ref _hideHotkeyId, 0x5057);
    }
    public OperationResult RegisterSearchHotkey(Hotkey hotkey) => RegisterShortcut(hotkey, ref _searchHotkeyId, 0x5059);
    private OperationResult RegisterShortcut(Hotkey hotkey, ref int registeredId, int firstId)
    {
        var newId = registeredId == firstId ? firstId + 1 : firstId;
        if (!Native.RegisterHotKey(_hwnd, newId, hotkey.Modifiers | 0x4000, hotkey.Key))
            return OperationResult.Fail("That shortcut is already in use. The previous shortcut remains active.");
        if (registeredId != 0) Native.UnregisterHotKey(_hwnd, registeredId);
        registeredId = newId;
        return OperationResult.Ok("Global shortcut registered.");
    }
    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (_controlCenter && message == 0x0083 && wParam != 0) return 0; // Borderless, full client area.
        if (_controlCenter && message == 0x0084 && Native.GetWindowRect(hwnd, out var border))
        {
            var packed = lParam.ToInt64(); var x = (short)(packed & 0xffff); var y = (short)((packed >> 16) & 0xffff);
            var grip = Math.Max(6, (int)Math.Round(7 * Native.GetDpiForWindow(hwnd) / 96d));
            var left = x < border.Left + grip; var right = x >= border.Right - grip;
            var top = y < border.Top + grip; var bottom = y >= border.Bottom - grip;
            if (top && left) return 13; if (top && right) return 14;
            if (bottom && left) return 16; if (bottom && right) return 17;
            if (left) return 10; if (right) return 11; if (top) return 12; if (bottom) return 15;
        }
        if (_controlCenter && message == 0x0024)
        {
            var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            var scale = Math.Max(96, Native.GetDpiForWindow(hwnd)) / 96d;
            limits.MinTrack.X = (int)(640 * scale); limits.MinTrack.Y = (int)(460 * scale);
            Marshal.StructureToPtr(limits, lParam, false); return 0;
        }
        if (_controlCenter && message == 0x0005 && wParam != 1 && !_layout && _hasFrame)
        {
            var resized = CurrentFrame with { DropAmount = 0 };
            ApplyFrame(resized); ControlCenterResized?.Invoke(resized);
        }
        if (message == 0x0312 && (int)wParam == _hotkeyId) { ToggleRequested?.Invoke(); return 0; }
        if (message == 0x0312 && (int)wParam == _hideHotkeyId) { HideRequested?.Invoke(); return 0; }
        if (message == 0x0312 && (int)wParam == _searchHotkeyId) { SearchRequested?.Invoke(); return 0; }
        if (message == 0x0021 && !_expanded) return 3; // Compact clicks do not activate until explicitly expanded.
        if (message == 0x0100 && wParam == 0x1B) EscapeRequested?.Invoke();
        if (message == 0x0231) _dragging = true;
        if (message == 0x007E || (message == 0x02E0 && !_dragging) || (message == 0x001A && wParam == 0x002F)) DisplayChanged?.Invoke();
        if (message == 0x0232 && Native.GetWindowRect(hwnd, out var rect))
        {
            _dragging = false;
            if (_controlCenter) return Native.DefSubclassProc(hwnd, message, wParam, lParam);
            var monitorHandle = Native.MonitorFromWindow(hwnd, 2);
            var monitor = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
            if (Native.GetMonitorInfo(monitorHandle, ref monitor))
            {
                var scale = Native.GetDpiForWindow(hwnd) / 96d;
                var work = monitor.Work;
                // Persist displacement from the chosen anchor in logical pixels.
                if (_position.Anchor is "Left edge" or "Right edge")
                {
                    var anchor = (rect.Left + rect.Width / 2d) < (monitor.Monitor.Left + monitor.Monitor.Right) / 2d ? "Left edge" : "Right edge";
                    _position = _position with { Monitor = monitor.Device, Anchor = anchor, OffsetX = 0, OffsetY = (rect.Top + rect.Height / 2d - (work.Top + work.Height / 2d)) / scale };
                    PositionChanged?.Invoke(_position);
                    return Native.DefSubclassProc(hwnd, message, wParam, lParam);
                }
                var baseX = _position.Anchor == "Bottom right" ? work.Right - rect.Width - 16 * scale
                    : _position.Anchor == "Bottom left" ? work.Left + 16 * scale : work.Left + (work.Width - rect.Width) / 2d;
                var baseY = _position.Anchor == "Top center" ? work.Top + 16 * scale : work.Bottom - rect.Height - 16 * scale;
                _position = _position with { Monitor = monitor.Device, OffsetX = (rect.Left - baseX) / scale, OffsetY = (rect.Top - baseY) / scale };
                PositionChanged?.Invoke(_position);
            }
        }
        return Native.DefSubclassProc(hwnd, message, wParam, lParam);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public Native.Point Reserved, MaxSize, MaxPosition, MinTrack, MaxTrack; }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_hotkeyId != 0) Native.UnregisterHotKey(_hwnd, _hotkeyId);
        if (_hideHotkeyId != 0) Native.UnregisterHotKey(_hwnd, _hideHotkeyId);
        if (_searchHotkeyId != 0) Native.UnregisterHotKey(_hwnd, _searchHotkeyId);
        Native.RemoveWindowSubclass(_hwnd, _callback, 1);
    }
}
