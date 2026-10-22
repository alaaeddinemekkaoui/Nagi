namespace Pulse.Core;

public static class ModePresets
{
    public static readonly string[] Names = ["Automatic", "Default", "Gaming", "Developer", "Creator", "Focus"];
    public static string For(ProfileWorkspace workspace) => workspace.Mode != "Automatic" ? workspace.Mode : workspace.Profile switch
    { Profile.Gaming => "Gaming", Profile.Work => "Developer", Profile.Stream => "Creator", Profile.Focus => "Focus", _ => "Default" };
    public static ProfileWorkspace Apply(ProfileWorkspace workspace, string mode) => workspace with
    {
        Mode = Names.Contains(mode) ? mode : "Automatic", ModeEnabled = true, ShowFps = mode == "Gaming",
        Widgets = mode switch
        {
            "Gaming" => ["Performance", "Microphone"],
            "Developer" => ["Performance", "Network", "Media", "Audio"],
            "Creator" => ["Performance", "Studio", "Microphone", "Media", "Audio"],
            "Focus" => ["Timer"],
            _ => ProfileWorkspaces.Default(workspace.Profile).Widgets
        }
    };
    public static string Description(string mode) => mode switch
    {
        "Gaming" => "FPS, temperature, game time and Discord desktop status.",
        "Developer" => "GitHub notifications, pinned repositories, build status and development tools.",
        "Creator" => "Recording, streaming, microphone levels, media and OBS controls.",
        "Focus" => "One task, your timer and the next calendar meeting. Quiet Nagi notifications.",
        _ => "Your everyday widgets and apps."
    };
}

public static class RepositoryName
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().TrimEnd('/');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        { if (uri.Scheme != "https" || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Query.Length > 0 || uri.Fragment.Length > 0) return null; value = uri.AbsolutePath.Trim('/'); }
        var parts = value.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length is > 0 and <= 100 && p is not "." and not ".." && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) ? value : null;
    }
}

// Measures time with the game in the foreground. Alt-tab pauses the active session.
public sealed class GameSessionClock
{
    private string? _identity;
    private DateTimeOffset? _last;
    public TimeSpan Elapsed { get; private set; }
    public string Game { get; private set; } = "";
    public void Observe(ForegroundApplication application, bool gaming, DateTimeOffset now)
    {
        if (!gaming || application.ProcessId <= 0) { _last = null; return; }
        var identity = application.ProcessId + ":" + application.ExecutablePath;
        if (_identity != identity) { _identity = identity; Elapsed = TimeSpan.Zero; _last = null; }
        if (_last is { } previous && now > previous && now - previous < TimeSpan.FromSeconds(30)) Elapsed += now - previous;
        Game = application.Name; _last = now;
    }
}
