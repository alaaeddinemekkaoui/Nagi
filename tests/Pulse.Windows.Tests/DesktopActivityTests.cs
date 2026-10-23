using System.Runtime.InteropServices;
using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class DesktopActivityTests
{
    private static AccessibleDesktopNode Node(string name = "", string id = "", int type = 50020, int parent = -1,
        double? value = null, double? min = null, double? max = null, bool hidden = false) => new(name, id, "", type, hidden, value, min, max, parent);
    [Fact] public void OnlyVisibleToastContainersProduceBanners()
    {
        Assert.Empty(DesktopActivityService.ParseBanners([Node("Private window title"), Node("An ordinary document", parent: 0)]));
        var nodes = new[] { Node(id: "ToastView", type: 50033), Node("Mail", "AppName", parent: 0), Node("New message", parent: 0), Node("Local test only", parent: 0) };
        var banner = Assert.Single(DesktopActivityService.ParseBanners(nodes));
        Assert.Equal("Mail", banner.App); Assert.Equal("New message", banner.Title); Assert.Equal("Local test only", banner.Body);
        Assert.Equal(banner.Key, Assert.Single(DesktopActivityService.ParseBanners(nodes)).Key);
        Assert.Empty(DesktopActivityService.ParseBanners(nodes.Select((n, i) => i == 0 ? n with { Offscreen = true } : n).ToArray()));
    }
    [Fact] public void ToastParserDoesNotIncludeTextOutsideTheToast()
    {
        var banner = Assert.Single(DesktopActivityService.ParseBanners([Node("Unrelated shell text"), Node(id: "ToastView", type: 50033), Node("Hello", parent: 1), Node("Secret outside", parent: 0)]));
        Assert.Equal("Hello", banner.Title); Assert.DoesNotContain("Secret", banner.Body);
    }
    [Fact] public void AVerifiedNativeBannerMayUseTheShellDismissControl()
    {
        var nodes = new[] { Node(type: 50032), Node("New banner", parent: 0), Node(id: "DismissButton", type: 50000, parent: 0) };
        Assert.Empty(DesktopActivityService.ParseBanners(nodes));
        Assert.Equal("New banner", Assert.Single(DesktopActivityService.ParseBanners(nodes, bannerWindow: true)).Title);
    }
    [Theory] [InlineData(0, 200, 50, 25)] [InlineData(10, 110, 60, 50)] [InlineData(0, 100, 100, 100)]
    public void TransferUsesTheActualAccessibleRange(double min, double max, double value, double expected) =>
        Assert.Equal(expected, DesktopActivityService.ParseTransfer([Node(type: 50012, value: value, min: min, max: max)]));
    [Theory] [InlineData("42% complete", 42)] [InlineData("Copie : 12,5 %", 12.5)] [InlineData("完成 89％", 89)] [InlineData("تم إكمال ٣٧٪", 37)] [InlineData("۱۲٫۵٪", 12.5)]
    public void TransferCanReadLocalizedPercentageFallback(string text, double expected) =>
        Assert.Equal(expected, DesktopActivityService.ParseTransfer([Node(text), Node(type: 50012)]));
    [Fact] public void OrdinaryDialogsAndInvalidProgressNeverInventATransfer()
    {
        Assert.Null(DesktopActivityService.ParseTransfer([Node("50% discount")]));
        Assert.Null(DesktopActivityService.ParseTransfer([Node(type: 50012, value: double.NaN, min: 0, max: 100)]));
        Assert.Null(DesktopActivityService.ParseTransfer([Node("999%"), Node(type: 50012)]));
    }
    [Fact] public void NativeShellDialogCanExposeOnlyItsCaptionAndHistoryIsNotABanner()
    {
        Assert.Equal(37, DesktopActivityService.ParseTransfer([Node("37% complete")], shellDialog: true));
        Assert.Null(DesktopActivityService.ParseTransfer([Node("Copying files")], shellDialog: true));
        Assert.Empty(DesktopActivityService.ParseBanners([Node(id: "NotificationCenter"), Node(id: "ToastView", parent: 0), Node("Old notification", parent: 1)]));
    }
    [Fact] public void NativeAccessibilityReadsARealWindowsProgressControl()
    {
        nint window = 0; uint threadId = 0; Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() => {
            try {
                threadId = GetCurrentThreadId(); var controls = new Controls { Size = 8, Classes = 0x20 }; InitCommonControlsEx(ref controls);
                window = CreateWindowEx(0, "STATIC", "Nagi owned accessibility fixture", unchecked((int)0x90000000), -2000, -2000, 240, 80, 0, 0, 0, 0);
                if (window == 0) throw new InvalidOperationException("Could not create fixture window");
                var progress = CreateWindowEx(0, "msctls_progress32", "", 0x50000000, 10, 10, 200, 25, window, 0, 0, 0);
                SendMessage(progress, 0x406, 0, 100); SendMessage(progress, 0x402, 38, 0); ready.Set();
                while (GetMessage(out var message, 0, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessage(ref message); }
            }
            catch (Exception ex) { failure = ex; ready.Set(); }
            finally { if (window != 0) DestroyWindow(window); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try {
            Assert.True(ready.Wait(10000)); Assert.Null(failure);
            var nodes = DesktopAccessibility.Read(window);
            Assert.Contains(nodes, n => n.Name == "Nagi owned accessibility fixture");
            Assert.Equal(38, DesktopActivityService.ParseTransfer(nodes));
        }
        finally { if (threadId != 0) PostThreadMessage(threadId, 0x12, 0, 0); Assert.True(thread.Join(5000)); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Controls { public uint Size, Classes; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("comctl32.dll")] private static extern bool InitCommonControlsEx(ref Controls controls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(int ex, string cls, string text, int style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
