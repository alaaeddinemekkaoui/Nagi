namespace Pulse.Core;

public interface ISystemMetricsService { SystemSnapshot Sample(); }
public interface IForegroundApplicationService { ForegroundApplication Sample(); }
public interface IMediaSessionService
{
    Task<MediaSnapshot> SampleAsync(CancellationToken cancellationToken);
    Task<OperationResult> SendAsync(string command);
}
public interface IAudioControlService
{
    AudioSnapshot Sample();
    OperationResult SetVolume(double percent);
    OperationResult SetMute(bool muted);
}
public interface ISettingsService
{
    string FilePath { get; }
    string? RecoveryMessage { get; }
    Task<PulseSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PulseSettings settings, CancellationToken cancellationToken = default);
}
public interface IProfileService { Profile Current { get; } }
public interface IContextEngine : IProfileService
{
    Profile Evaluate(ForegroundApplication application, PulseSettings settings, DateTimeOffset now);
}
public interface IWindowManager : IDisposable
{
    void SetLayout(bool expanded, bool dashboard, HaloPosition position);
    void SetVisible(bool visible);
    OperationResult RegisterHotkey(Hotkey hotkey);
    OperationResult RegisterHideHotkey(Hotkey hotkey);
}
public interface INotificationService { void Publish(string message, bool essential = false); }
public interface IDiagnosticLog { void Write(string category, string message, Exception? exception = null); }
