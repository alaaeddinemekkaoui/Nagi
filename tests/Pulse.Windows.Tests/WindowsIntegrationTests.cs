using System.Runtime.InteropServices;
using Pulse.Core;
using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class WindowsIntegrationTests
{
    [Fact] public void SpeakerEnumerationUsesConnectedEndpointsAndRejectsUnknownDevices()
    {
        using var service = new AudioControlService();
        var devices = service.GetOutputDevices();
        Assert.All(devices, device => { Assert.False(string.IsNullOrWhiteSpace(device.Id)); Assert.False(string.IsNullOrWhiteSpace(device.Name)); });
        Assert.InRange(devices.Count(d => d.IsDefault), 0, 1);
        Assert.False(service.SetOutputDevice("Nagi.missing.speaker").Success);
        // Writing the existing speaker exercises routing without changing the user's selected endpoint.
        if (devices.FirstOrDefault(d => d.IsDefault) is { } current)
        {
            Assert.True(service.SetOutputDevice(current.Id, multimediaOnly: true).Success);
            Assert.Equal(current.Id, Assert.Single(service.GetOutputDevices(), d => d.IsDefault).Id);
        }
    }
    [Fact] public async Task InstalledApplicationsComeFromWindowsAndUnknownEntriesCannotLaunch()
    {
        var catalog = await SearchApplications.ReadAsync(default);
        Assert.True(catalog.Error is null, catalog.Diagnostic ?? catalog.Error); Assert.NotEmpty(catalog.Entries);
        Assert.All(catalog.Entries, entry => { Assert.Equal(SearchKind.Application, entry.Kind); Assert.False(string.IsNullOrWhiteSpace(entry.Target)); });
        var stale = new SearchEntry("not-installed", "Missing", "", SearchKind.Application, "Pulse.Nonexistent.Test.9f0bd4");
        Assert.False((await SearchApplications.OpenAsync(stale, default)).Success);
    }
    [Fact] public void SearchShortcutConflictsDoNotReplacePreviousRegistrationAndDisposeReleasesIt()
    {
        var window1 = CreateWindowEx(0, "STATIC", "Pulse search test 1", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        var window2 = CreateWindowEx(0, "STATIC", "Pulse search test 2", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        try
        {
            using var first = new HaloWindowManager(window1); using var second = new HaloWindowManager(window2);
            Hotkey? key = null;
            foreach (var candidate in Enumerable.Range(0x7C, 12)) if (first.RegisterSearchHotkey(new(7, (uint)candidate)).Success) { key = new(7, (uint)candidate); break; }
            Assert.NotNull(key); Assert.False(second.RegisterSearchHotkey(key).Success);
            Assert.False(first.RegisterHideHotkey(key).Success);
            first.Dispose(); Assert.True(second.RegisterSearchHotkey(key).Success);
        }
        finally { DestroyWindow(window1); DestroyWindow(window2); }
    }
    [Fact] public void CachedAudioSamplingReturnsRealBoundedPeakAndReleasesCleanly()
    {
        using var audio = new AudioControlService();
        for (var i = 0; i < 30; i++)
        {
            var sample = audio.Sample();
            if (sample.Volume is { } volume) Assert.InRange(volume, 0, 100);
            if (sample.Peak is { } peak) Assert.InRange(peak, 0, 1);
            if (!sample.Volume.HasValue) Assert.NotNull(sample.Error);
        }
    }
    [Fact] public void DisposedAudioAndInvalidVolumeDoNotReactivateEndpoint()
    {
        var audio = new AudioControlService();
        Assert.False(audio.SetVolume(double.NaN).Success);
        audio.Dispose(); audio.Dispose();
        Assert.Null(audio.Sample().Volume);
        Assert.False(audio.SetMute(false).Success);
    }
    [Fact] public void RealPowerSnapshotHasValidPercentageOrExplicitlyUnavailableStatus()
    {
        var power = Assert.IsType<PowerSnapshot>(new SystemMetricsService().Sample().Power);
        if (power.Percent is { } percent) Assert.InRange(percent, 0, 100);
        if (power.HasBattery == false) { Assert.Null(power.Percent); Assert.Equal("AC", power.CompactText); }
        if (power.Charging == true) Assert.True(power.HasBattery);
        Assert.False(string.IsNullOrWhiteSpace(power.Detail));
    }
    [Fact] public async Task RealMetricsProduceValidCpuAndMemorySamples()
    {
        var service = new SystemMetricsService(); service.Sample(); await Task.Delay(200);
        var sample = service.Sample();
        Assert.NotNull(sample.Cpu); Assert.InRange(sample.Cpu.Value, 0, 100);
        Assert.True(sample.TotalMemory > 0); Assert.InRange(sample.AvailableMemory, 0UL, sample.TotalMemory);
        Assert.InRange(sample.MemoryPercent!.Value, 0, 100);
        if (sample.ReceiveBytesPerSecond.HasValue) Assert.True(sample.ReceiveBytesPerSecond >= 0);
    }
    [Fact] public void ForegroundReadRequiresNoElevation()
    {
        var sample = new ForegroundApplicationService().Sample();
        Assert.True(sample.ProcessId >= 0); Assert.False(string.IsNullOrWhiteSpace(sample.Name));
    }
    [Fact] public void AudioReportsRealEndpointOrExplicitUnavailableState()
    {
        using var service = new AudioControlService(); var sample = service.Sample();
        Assert.False(string.IsNullOrWhiteSpace(sample.Device));
        if (sample.Volume.HasValue) Assert.InRange(sample.Volume.Value, 0, 100);
        else Assert.NotNull(sample.Error);
    }
    [Fact] public async Task MediaReadIsBoundedAndHasHonestEmptyState()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sample = await new MediaSessionService().SampleAsync(timeout.Token);
        Assert.False(string.IsNullOrWhiteSpace(sample.Title));
        if (!sample.Available) Assert.False(sample.CanPlayPause || sample.CanNext || sample.CanPrevious);
    }
    [Fact] public void AudioCanWriteExistingValuesWithoutChangingUserVolumeOrMute()
    {
        using var audio = new AudioControlService(); var before = audio.Sample();
        if (!before.Volume.HasValue) { Assert.NotNull(before.Error); return; }
        Assert.True(audio.SetVolume(before.Volume.Value).Success);
        Assert.True(audio.SetMute(before.Muted).Success);
        var after = audio.Sample();
        Assert.NotNull(after.Volume);
        Assert.Equal(before.Volume.Value, after.Volume.Value, 2);
        Assert.Equal(before.Muted, after.Muted);
    }
    [Fact] public void HotkeyConflictKeepsPreviousRegistrationAndDisposeReleasesIt()
    {
        var hwnd1 = CreateWindowEx(0, "STATIC", "Pulse test 1", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        var hwnd2 = CreateWindowEx(0, "STATIC", "Pulse test 2", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, hwnd1); Assert.NotEqual(0, hwnd2);
        try
        {
            using var manager1 = new HaloWindowManager(hwnd1);
            using var manager2 = new HaloWindowManager(hwnd2);
            Hotkey? shortcut = null;
            foreach (var key in Enumerable.Range(0x7C, 12))
                if (manager1.RegisterHotkey(new(7, (uint)key)).Success) { shortcut = new(7, (uint)key); break; }
            Assert.NotNull(shortcut);
            Assert.False(manager2.RegisterHotkey(shortcut).Success);
            manager1.Dispose();
            Assert.True(manager2.RegisterHotkey(shortcut).Success);
        }
        finally { DestroyWindow(hwnd1); DestroyWindow(hwnd2); }
    }
    [Fact] public void MissingMonitorAndLargeOffsetsAreClampedToWorkArea()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Pulse placement test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, hwnd);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            manager.SetLayout(true, true, new("missing display", "Bottom center", 100000, -100000));
            Assert.True(GetWindowRect(hwnd, out var rect));
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor));
            Assert.True(rect.Left >= monitor.Work.Left && rect.Top >= monitor.Work.Top);
            Assert.True(rect.Right <= monitor.Work.Right && rect.Bottom <= monitor.Work.Bottom);
            Assert.False(IsWindowVisible(hwnd)); // Repositioning a hidden Halo must not reveal it.
        }
        finally { DestroyWindow(hwnd); }
    }
    [Theory]
    [InlineData("Bottom center")]
    [InlineData("Top center")]
    [InlineData("Bottom left")]
    [InlineData("Bottom right")]
    public void HoverPreviewKeepsCompactStripAtItsAnchorWithoutShowingTheWindow(string anchor)
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Pulse preview placement test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, hwnd);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var position = new HaloPosition(Anchor: anchor);
            var compact = manager.PrepareLayout(false, false, position);
            var preview = manager.PrepareLayout(false, false, position, 306);
            Assert.Equal(compact.X, preview.X);
            Assert.Equal(compact.Width, preview.Width);
            Assert.True(preview.Height > compact.Height);
            if (anchor == "Top center") Assert.Equal(compact.Y, preview.Y);
            else Assert.Equal(compact.Y + compact.Height, preview.Y + preview.Height);
            manager.ApplyFrame(preview);
            Assert.False(manager.IsVisible);
        }
        finally { DestroyWindow(hwnd); }
    }
    [Theory]
    [InlineData("Left edge", 0)]
    [InlineData("Right edge", 0)]
    [InlineData("Left edge", -100000)]
    [InlineData("Right edge", -100000)]
    [InlineData("Left edge", 100000)]
    [InlineData("Right edge", 100000)]
    public void EdgeDropAndRevealsStayConnectedAndPreserveTheCompactStrip(string anchor, double offset)
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Pulse edge placement test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, hwnd);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var position = new HaloPosition(Anchor: anchor, OffsetY: offset);
            var drop = manager.PrepareLayout(false, false, position, 84, true);
            manager.ApplyFrame(drop);
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor));
            var compact = manager.PrepareLayout(false, false, position, 84);
            var preview = manager.PrepareLayout(false, false, position, 336);
            Assert.InRange(Math.Abs(compact.Y + compact.Height / 2 - (preview.Y + preview.Height / 2)), 0, 1);
            Assert.Equal(80 * compact.Scale, compact.Width, 0);
            Assert.Equal(compact.Height, preview.Height);
            Assert.True(preview.Width > compact.Width);
            foreach (var frame in new[] { drop, compact, preview, manager.PrepareLayout(true, false, position) })
            {
                Assert.Equal(anchor == "Left edge" ? monitor.Monitor.Left : monitor.Monitor.Right, anchor == "Left edge" ? frame.X : frame.X + frame.Width);
                Assert.InRange(frame.Y, monitor.Work.Top, monitor.Work.Bottom - frame.Height);
            }
            Assert.True(drop.Width < compact.Width);
            Assert.False(manager.IsVisible);
        }
        finally { DestroyWindow(hwnd); }
    }
    [Theory]
    [InlineData("Top edge", 0)]
    [InlineData("Bottom edge", 0)]
    [InlineData("Top edge", -100000)]
    [InlineData("Bottom edge", 100000)]
    public void HorizontalDropAndRevealsStayAboveTheTaskbarWithClampedOffsets(string anchor, double offset)
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Nagi horizontal layout test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            var position = new HaloPosition(Anchor: anchor, OffsetX: offset, Orientation: "Horizontal");
            var compact = manager.PrepareLayout(false, false, position, 84);
            manager.ApplyFrame(compact);
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor));
            var frames = new[] { manager.PrepareLayout(false, false, position, 84, true), compact,
                manager.PrepareLayout(false, false, position, 336), manager.PrepareLayout(true, true, position) };
            Assert.Equal(76 * compact.Scale, compact.Height, 0);
            foreach (var frame in frames)
            {
                Assert.InRange(frame.X, monitor.Work.Left, monitor.Work.Right - frame.Width);
                Assert.InRange(frame.Y, monitor.Work.Top, monitor.Work.Bottom - frame.Height);
                Assert.Equal(anchor == "Top edge" ? monitor.Work.Top : monitor.Work.Bottom,
                    anchor == "Top edge" ? frame.Y : frame.Y + frame.Height);
                Assert.InRange(Math.Abs(compact.X + compact.Width / 2 - (frame.X + frame.Width / 2)), 0, 1);
            }
            Assert.False(manager.IsVisible);
        }
        finally { DestroyWindow(hwnd); }
    }
    [Fact] public void NativeFrameRemovalLetsTheClientFillTheWholeEdgeWindow()
    {
        var hwnd = CreateWindowEx(0, "STATIC", "Pulse border test", 0x80C40000, 0, 0, 200, 100, 0, 0, 0, 0);
        Assert.NotEqual(0, hwnd);
        try
        {
            using var manager = new HaloWindowManager(hwnd);
            Assert.True(GetWindowRect(hwnd, out var window));
            Assert.True(GetClientRect(hwnd, out var client));
            Assert.Equal(window.Right - window.Left, client.Right - client.Left);
            Assert.Equal(window.Bottom - window.Top, client.Bottom - client.Top);
            Assert.False(manager.IsVisible);
        }
        finally { DestroyWindow(hwnd); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
}
