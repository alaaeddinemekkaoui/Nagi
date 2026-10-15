using System.Runtime.InteropServices;
using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class TrayIntegrationTests
{
    [Fact] public void RealTrayRegistrationCanBeRecreatedAndIsRemovedOnDispose()
    {
        var window = CreateWindowEx(0, "STATIC", "Pulse tray integration test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, window);
        try
        {
            using var tray = new SystemTrayService(window, Path.Combine(AppContext.BaseDirectory, "Nagi.Tray.ico"), Guid.NewGuid());
            Assert.True(tray.Register().Success); Assert.True(tray.IsRegistered);
            Assert.True(tray.TryGetBounds(out _));
            Assert.True(tray.Register().Success); // Same path used after Explorer recreates the taskbar.
            Assert.True(tray.TryGetBounds(out _));
            tray.Dispose();
            Assert.False(tray.IsRegistered); Assert.False(tray.TryGetBounds(out _));
        }
        finally { DestroyWindow(window); }
    }
    [Fact] public void HideShortcutIsIndependentAndConflictsDoNotReplaceTheToggleShortcut()
    {
        var window = CreateWindowEx(0, "STATIC", "Pulse hide shortcut test", 0x80000000, 0, 0, 20, 20, 0, 0, 0, 0);
        Assert.NotEqual(0, window);
        try
        {
            using var manager = new HaloWindowManager(window);
            Pulse.Core.Hotkey? toggle = null, hide = null;
            foreach (var key in Enumerable.Range(0x7C, 12))
                if (manager.RegisterHotkey(new(7, (uint)key)).Success) { toggle = new(7, (uint)key); break; }
            Assert.NotNull(toggle);
            Assert.False(manager.RegisterHideHotkey(toggle).Success);
            foreach (var key in Enumerable.Range(0x7C, 12).Where(k => k != toggle.Key))
                if (manager.RegisterHideHotkey(new(7, (uint)key)).Success) { hide = new(7, (uint)key); break; }
            Assert.NotNull(hide);
            Assert.False(manager.RegisterHotkey(hide).Success);
        }
        finally { DestroyWindow(window); }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hwnd);
}
