using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Pulse.Windows;

public sealed record DesktopNotification(uint Id, string App, string Title, string Body, DateTimeOffset Created);

public sealed class WindowsNotificationService : IDisposable
{
    private UserNotificationListener? _listener;
    public event Action? Changed;
    public static bool HasIdentity { get { try { return Package.Current is not null; } catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { return false; } } }
    public string Status { get; private set; } = "Windows notifications are off";
    public bool Allowed { get { try { return HasIdentity && (_listener ??= UserNotificationListener.Current).GetAccessStatus() == UserNotificationListenerAccessStatus.Allowed; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException) { return false; } } }
    public async Task<bool> RequestAccessAsync()
    {
        if (!HasIdentity) { Status = "Windows requires a packaged Nagi build for access to other apps' notifications."; return false; }
        try
        {
            _listener ??= UserNotificationListener.Current;
            var status = await _listener.RequestAccessAsync();
            Status = status == UserNotificationListenerAccessStatus.Allowed ? "Connected to Windows notifications" : "Allow notification access in Windows Settings, then reconnect.";
            return status == UserNotificationListenerAccessStatus.Allowed;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        { Status = "Windows notification access is unavailable. Check package capabilities and Windows privacy settings."; return false; }
    }
    public void Listen()
    {
        Stop(); if (!Allowed) return;
        _listener!.NotificationChanged += NotificationChanged;
    }
    private void NotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args) => Changed?.Invoke();
    public async Task<DesktopNotification[]> ReadAsync()
    {
        try
        {
            if (!Allowed) { Status = HasIdentity ? "Notification access is off in Windows Settings" : "Install a packaged build to connect Windows notifications"; return []; }
            var notifications = await _listener!.GetNotificationsAsync(NotificationKinds.Toast);
            Status = "Connected to Windows notifications";
            return notifications.OrderByDescending(n => n.CreationTime).Take(30).Select(n =>
            {
                var text = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements().Select(t => t.Text).ToArray() ?? [];
                return new DesktopNotification(n.Id, n.AppInfo.DisplayInfo.DisplayName, text.FirstOrDefault() ?? "Notification",
                    Limit(string.Join("\n", text.Skip(1))), n.CreationTime);
            }).ToArray();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        { Status = "Windows notification access is unavailable or was revoked"; return []; }
    }
    public void Stop() { if (_listener is not null) _listener.NotificationChanged -= NotificationChanged; }
    private static string Limit(string value) => value.Length > 4096 ? value[..4096] : value;
    public void Dispose() => Stop();
}
