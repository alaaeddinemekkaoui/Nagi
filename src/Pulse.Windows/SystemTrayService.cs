using System.Runtime.InteropServices;
using Pulse.Core;

namespace Pulse.Windows;

public readonly record struct TrayIconBounds(int X, int Y, int Width, int Height);
/// <summary>Native notification-area integration. The icon survives Halo hiding and Explorer restarts.</summary>
public sealed class SystemTrayService : IDisposable
{
    private const uint CallbackMessage = 0x8050;
    private readonly nint _hwnd;
    private readonly nint _icon;
    private readonly Native.SubclassProc _callback;
    private readonly uint _taskbarCreated;
    private NotifyIconData _data;
    private bool _disposed, _version4;
    private string _tooltip = "";
    private Profile? _lockedProfile;
    private string _hideShortcut = "Ctrl + Alt + H";
    private uint _accessibleMenuCommand;
    public bool IsRegistered { get; private set; }
    public bool IsMenuOpen { get; private set; }
    public event Action? ShowRequested;
    public event Action? HideRequested;
    public event Action? SettingsRequested;
    public event Action? SearchRequested;
    public event Action? QuitRequested;
    public event Action? MenuOpening;
    public event Action<Profile?>? ProfileRequested;
    public event Action<OperationResult>? RegistrationChanged;

    public SystemTrayService(nint hwnd, string iconPath, Guid? iconId = null)
    {
        _hwnd = hwnd;
        _icon = LoadImage(0, iconPath, 1, 32, 32, 0x0010);
        if (_icon == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Nagi tray icon could not be loaded.");
        _callback = WindowProcedure;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        if (!Native.SetWindowSubclass(hwnd, _callback, 2, 0))
        {
            DestroyIcon(_icon);
            throw new InvalidOperationException("Nagi tray message handler could not be attached.");
        }
        _data = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = hwnd, Id = 1,
            Flags = 0x1 | 0x2 | 0x4 | 0x20 | 0x80, Callback = CallbackMessage, Icon = _icon,
            Tip = "Nagi · Your desktop, in rhythm", Info = "", InfoTitle = "",
            // Unsigned portable apps need an identity tied to their executable location.
            Guid = iconId ?? new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                (Environment.ProcessPath ?? AppContext.BaseDirectory).ToUpperInvariant()))[..16])
        };
    }
    public OperationResult Register()
    {
        if (_disposed) return OperationResult.Fail("Tray service is closed.");
        if (IsRegistered) ShellNotifyIcon(2, ref _data);
        IsRegistered = ShellNotifyIcon(0, ref _data);
        if (!IsRegistered) return OperationResult.Fail("Windows notification area is unavailable. Nagi can still be recovered by launching Nagi.");
        _data.Version = 4;
        _version4 = ShellNotifyIcon(4, ref _data);
        return OperationResult.Ok("Nagi is available in the system tray.");
    }
    public void Update(Profile profile, Profile? lockedProfile, bool hidden, string hideShortcut)
    {
        _lockedProfile = lockedProfile; _hideShortcut = hideShortcut;
        var tooltip = $"Nagi · {profile}\n{(hidden ? "Nagi hidden" : "Nagi ready")} · {hideShortcut}\nRight-click for controls";
        if (_tooltip == tooltip || !IsRegistered || _disposed) return;
        _tooltip = tooltip; _data.Tip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        if (!ShellNotifyIcon(1, ref _data))
        {
            IsRegistered = false;
            RegistrationChanged?.Invoke(OperationResult.Fail("Windows could not update the Nagi tray icon."));
        }
    }
    public bool TryGetBounds(out TrayIconBounds bounds)
    {
        var identity = new NotifyIconIdentifier { Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(), Window = _hwnd, Id = 1, Guid = _data.Guid };
        var success = ShellNotifyIconGetRect(ref identity, out var rect) >= 0;
        bounds = new(rect.Left, rect.Top, rect.Width, rect.Height);
        return success;
    }
    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (_disposed) return Native.DefSubclassProc(hwnd, message, wParam, lParam);
        // Native accessibility clients may send WM_COMMAND instead of selecting through TrackPopupMenuEx.
        if (message == 0x0111 && IsMenuOpen && lParam == 0)
        {
            var command = (uint)((ulong)wParam & 0xFFFF);
            if (command is >= 1 and <= 5 or >= 10 and <= 15)
            { _accessibleMenuCommand = command; EndMenu(); return 0; }
        }
        if (_taskbarCreated != 0 && message == _taskbarCreated)
        {
            RegistrationChanged?.Invoke(Register());
            return 0;
        }
        if (message == CallbackMessage)
        {
            var notification = _version4 ? (uint)(lParam.ToInt64() & 0xFFFF) : (uint)lParam.ToInt64();
            if (notification is 0x0400 or 0x0401 || (!_version4 && notification == 0x0202)) ShowRequested?.Invoke();
            if (notification == 0x007B || (!_version4 && notification == 0x0205))
            {
                var x = _version4 ? (short)((ulong)wParam & 0xFFFF) : -1;
                var y = _version4 ? (short)(((ulong)wParam >> 16) & 0xFFFF) : -1;
                ShowMenu(x, y);
            }
            return 0;
        }
        return Native.DefSubclassProc(hwnd, message, wParam, lParam);
    }
    private void ShowMenu(int x, int y)
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        IsMenuOpen = true;
        _accessibleMenuCommand = 0;
        uint selected;
        try
        {
            MenuOpening?.Invoke();
            AppendMenu(menu, 0, 1, "Open Nagi");
            AppendMenu(menu, 0, 5, "Quick search");
            AppendMenu(menu, 0, 2, "Hide Nagi\t" + _hideShortcut);
            AppendMenu(menu, 0, 3, "Settings…");
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, _lockedProfile is null ? 8u : 0u, 10, "Automatic profile");
            foreach (var profile in ProfileWorkspaces.Profiles)
                AppendMenu(menu, _lockedProfile == profile ? 8u : 0u, (nuint)(11 + (int)profile), profile.ToString());
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, 0, 4, "Quit Nagi");
            if (x == -1 || y == -1) { GetCursorPos(out var point); x = point.X; y = point.Y; }
            SetForegroundWindow(_hwnd);
            selected = TrackPopupMenuEx(menu, 0x0100 | 0x0002, x, y, _hwnd, 0);
            if (_accessibleMenuCommand != 0) selected = _accessibleMenuCommand;
            PostMessage(_hwnd, 0, 0, 0); // Ensure the shell can dismiss the next menu correctly.
        }
        finally { DestroyMenu(menu); IsMenuOpen = false; }
        switch (selected)
        {
            case 1: ShowRequested?.Invoke(); break;
            case 2: HideRequested?.Invoke(); break;
            case 3: SettingsRequested?.Invoke(); break;
            case 4: QuitRequested?.Invoke(); break;
            case 5: SearchRequested?.Invoke(); break;
            case 10: ProfileRequested?.Invoke(null); break;
            case >= 11 and <= 15: ProfileRequested?.Invoke((Profile)(selected - 11)); break;
            default: ShellNotifyIcon(3, ref _data); break;
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsRegistered) ShellNotifyIcon(2, ref _data);
        IsRegistered = false;
        Native.RemoveWindowSubclass(_hwnd, _callback, 2);
        DestroyIcon(_icon);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct NotifyIconData
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NotifyIconIdentifier { public uint Size; public nint Window; public uint Id; public Guid Guid; }
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")] private static extern int ShellNotifyIconGetRect(ref NotifyIconIdentifier identity, out Native.Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string text);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EndMenu();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);
}
