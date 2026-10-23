using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Pulse.Windows;

public sealed record DesktopBanner(string Key, string App, string Title, string Body);
public sealed record WindowsFileTransfer(nint Window, double? Percent);
public sealed record DesktopActivitySnapshot(DesktopBanner[] Banners, WindowsFileTransfer[] Transfers);
internal sealed record AccessibleDesktopNode(string Name, string Id, string Class, int Type, bool Offscreen, double? Value, double? Minimum, double? Maximum, int Parent);

// Read-only accessibility observation. No shell injection, disk tracing, clipboard access or file copying.
public sealed class DesktopActivityService : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly AutoResetEvent _wake = new(false);
    private Thread? _thread;
    private int _disposed;
    private long _configuration;
    private volatile bool _banners, _transfers;
    public event Action<DesktopActivitySnapshot>? Changed;
    public void Configure(bool banners, bool transfers)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _banners = banners; _transfers = transfers; Interlocked.Increment(ref _configuration);
        if (_thread is null && (banners || transfers)) {
            _thread = new Thread(Run) { IsBackground = true, Name = "Nagi desktop activity" };
            _thread.SetApartmentState(ApartmentState.MTA); _thread.Start();
        }
        _wake.Set();
    }
    private void Run()
    {
        try { RunLoop(); }
        finally { _wake.Dispose(); _stop.Dispose(); }
    }
    private void RunLoop()
    {
        string previous = "";
        while (!_stop.IsCancellationRequested) {
            try {
                var configuration = Interlocked.Read(ref _configuration);
                var snapshot = _banners || _transfers ? Capture(_banners, _transfers) : new DesktopActivitySnapshot([], []);
                if (configuration != Interlocked.Read(ref _configuration)) continue;
                var key = string.Join('|', snapshot.Banners.Select(b => b.Key)) + ":" + string.Join('|', snapshot.Transfers.Select(t => t.Window + ":" + t.Percent));
                if (key != previous) { previous = key; Changed?.Invoke(snapshot); }
                _wake.WaitOne(snapshot.Transfers.Length > 0 ? 500 : 1000);
            }
            catch (Exception ex) when (ex is COMException or InvalidComObjectException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException) { _wake.WaitOne(2000); }
        }
    }
    internal static DesktopActivitySnapshot Capture(bool banners, bool transfers)
    {
        var foundBanners = new List<DesktopBanner>(); var foundTransfers = new List<WindowsFileTransfer>();
        var candidates = new List<(nint Handle, bool Banner, bool ShellCopy)>();
        EnumWindows((window, _) => {
            if (!IsWindowVisible(window) || !GetWindowRect(window, out var rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return true;
            var windowClass = WindowClass(window);
            if (!banners && (!transfers || windowClass is not ("OperationStatusWindow" or "#32770"))) return true;
            GetWindowThreadProcessId(window, out var pid);
            try {
                using var process = Process.GetProcessById((int)pid);
                var shell = banners && (process.ProcessName is "ShellExperienceHost" or "ShellHost") && IsBannerWindow(window, rect);
                var shellCopy = transfers && windowClass == "OperationStatusWindow";
                var copy = shellCopy || transfers && process.ProcessName == "explorer" && windowClass == "#32770";
                if (!shell && !copy) return true;
                var path = process.MainModule?.FileName;
                var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + Path.DirectorySeparatorChar;
                if (shellCopy || path is not null && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) candidates.Add((window, shell, shellCopy));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return candidates.Count < 12;
        }, 0);
        foreach (var candidate in candidates) {
            try {
                // Modern Shell copy dialogs may expose the percentage only in the native caption.
                if (candidate.ShellCopy && ParseTransfer([new(WindowTitle(candidate.Handle), "", "", 50032, false, null, null, null, -1)], shellDialog: true) is { } nativePercent) {
                    foundTransfers.Add(new(candidate.Handle, nativePercent)); continue;
                }
                var nodes = DesktopAccessibility.Read(candidate.Handle);
                if (candidate.Banner) foundBanners.AddRange(ParseBanners(nodes, bannerWindow: true));
                else if (ParseTransfer(nodes, candidate.ShellCopy) is { } value) foundTransfers.Add(new(candidate.Handle, value));
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException) { }
        }
        return new(foundBanners.DistinctBy(b => b.Key).Take(8).ToArray(), foundTransfers.ToArray());
    }
    internal static DesktopBanner[] ParseBanners(IReadOnlyList<AccessibleDesktopNode> nodes, bool bannerWindow = false)
    {
        var result = new List<DesktopBanner>();
        if (nodes.Any(n => n.Id.Contains("NotificationCenter", StringComparison.OrdinalIgnoreCase) || n.Id == "ActionCenter")) return [];
        for (var i = 0; i < nodes.Count; i++) {
            var root = nodes[i];
            var knownToast = root.Id is "ToastView" or "ToastContent" or "NotificationToast" || root.Class is "ToastView" or "ToastContentHost";
            var nativeToast = i == 0 && bannerWindow && nodes.Any(n => n.Id == "DismissButton" && !n.Offscreen);
            if (root.Offscreen || !knownToast && !nativeToast) continue;
            bool Inside(int index) { for (var depth = 0; index >= 0 && depth < 16; depth++, index = nodes[index].Parent) if (index == i) return true; return false; }
            var text = nodes.Where((n, index) => Inside(index) && !n.Offscreen && n.Type == 50020 && !string.IsNullOrWhiteSpace(n.Name)).ToArray();
            var app = text.FirstOrDefault(n => n.Id.Contains("AppName", StringComparison.OrdinalIgnoreCase))?.Name ?? "Windows";
            var lines = text.Where(n => n.Name != app).Select(n => Limit(n.Name, 512)).Distinct().Take(5).ToArray();
            if (lines.Length == 0) continue;
            var title = lines[0]; var body = string.Join("\n", lines.Skip(1));
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(app + "\n" + title + "\n" + body)));
            result.Add(new(key, Limit(app, 128), title, body));
        }
        return result.DistinctBy(b => b.Key).ToArray();
    }
    internal static double? ParseTransfer(IReadOnlyList<AccessibleDesktopNode> nodes, bool shellDialog = false)
    {
        var bars = nodes.Where(n => n.Type == 50012).ToArray();
        if (bars.Length == 0 && !shellDialog) return null; // Ordinary dialogs are not file transfers.
        foreach (var bar in bars) if (bar.Value is { } value && bar.Minimum is { } min && bar.Maximum is { } max && double.IsFinite(value) && max > min && value >= min && value <= max)
            return Math.Clamp((value - min) / (max - min) * 100, 0, 100);
        foreach (var node in nodes) {
            var match = Regex.Match(node.Name, @"(?<![\d.,٫])(?<value>\d{1,3}(?:[.,٫]\d+)?)\s*[%％٪]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            if (!match.Success) continue;
            var number = new string(match.Groups["value"].Value.Select(c => char.IsDigit(c) ? (char)('0' + CharUnicodeInfo.GetDigitValue(c)) : c is ',' or '٫' ? '.' : c).ToArray());
            if (double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 100) return value;
        }
        return null;
    }
    private static string Limit(string value, int count) => value.Length > count ? value[..count] : value;
    private static string WindowClass(nint window) { var text = new StringBuilder(128); GetClassName(window, text, text.Capacity); return text.ToString(); }
    private static string WindowTitle(nint window) { var text = new StringBuilder(512); GetWindowText(window, text, text.Capacity); return text.ToString(); }
    private static bool IsBannerWindow(nint window, Rect rect)
    {
        var info = new Monitor { Size = (uint)Marshal.SizeOf<Monitor>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info)) return false;
        var scale = Math.Max(1, GetDpiForWindow(window) / 96d);
        var width = (rect.Right - rect.Left) / scale; var height = (rect.Bottom - rect.Top) / scale;
        // Only the small default toast surface, not the notification history or other shell panels.
        return width is >= 120 and <= 720 && height is >= 48 and <= 520
            && rect.Bottom - rect.Top < (info.Bounds.Bottom - info.Bounds.Top) * .6
            && Math.Abs(rect.Right - info.Bounds.Right) <= 64 * scale
            && rect.Top >= info.Bounds.Top + (info.Bounds.Bottom - info.Bounds.Top) / 2;
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel(); try { _wake.Set(); } catch (ObjectDisposedException) { } // A provider timeout never blocks the UI during shutdown.
        if (_thread is null) { _wake.Dispose(); _stop.Dispose(); }
    }
    private delegate bool EnumWindowProc(nint window, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Monitor { public uint Size; public Rect Bounds, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref Monitor info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
}
