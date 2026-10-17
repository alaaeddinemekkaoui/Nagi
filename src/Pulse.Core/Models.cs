using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulse.Core;

// Keep the original numeric values for existing settings.
public enum Profile { Work, Gaming, Focus, Default, Stream }
public enum ActionKind { Application, Folder, File, Url, Profile, ToggleHalo }
public sealed record AppRule(string Identity, Profile Profile, int Priority = 100, bool Enabled = true);
public sealed record QuickAction(string Label, ActionKind Kind, string Target, Profile? Profile = null);
public sealed record Hotkey(uint Modifiers = 0x0003, uint Key = 0x20); // Alt+Ctrl+Space
public sealed record HaloPosition(string Monitor = "", string Anchor = "Right edge", double OffsetX = 0, double OffsetY = 0, string Orientation = "Vertical");

public sealed record PulseSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string Theme { get; init; } = "System";
    public string AkenoBackground { get; init; } = "Samurai";
    public ProfileWorkspace[] Workspaces { get; init; } = ProfileWorkspaces.Normalize(null);
    public ProfileWorkspace Workspace(Profile profile) => Workspaces.FirstOrDefault(w => w.Profile == profile) ?? ProfileWorkspaces.Default(profile);
    public bool WelcomeCompleted { get; init; }
    public string UserName { get; init; } = "";
    public bool WeatherEnabled { get; init; }
    public WeatherLocation? WeatherLocation { get; init; }
    public string WeatherUnit { get; init; } = "Celsius";
    public HaloPosition Position { get; init; } = new();
    public bool StartExpanded { get; init; }
    public Hotkey Hotkey { get; init; } = new();
    public Hotkey HideHotkey { get; init; } = new(3, 0x48); // Ctrl+Alt+H
    public Hotkey SearchHotkey { get; init; } = new(1, 0x20); // Alt+Space
    public bool IndexFiles { get; init; } = true;
    public bool FpsEnabled { get; init; }
    public bool ObsEnabled { get; init; }
    public int ObsPort { get; init; } = 4455;
    public string[] ObsScenes { get; init; } = ["", "", ""];
    public string PreferredMusicPlayer { get; init; } = "All";
    public bool AutomaticUpdates { get; init; } = true;
    public string[] SearchFolders { get; init; } = [];
    public bool GlassEnabled { get; init; } = true;
    public bool LuaUiEnabled { get; init; }
    public int PollingIntervalMs { get; init; } = 1500;
    public Profile DefaultProfile { get; init; } = Profile.Default;
    public Profile? LockedProfile { get; init; }
    public bool AutomaticSwitching { get; init; } = true;
    public bool CloseOnDeactivate { get; init; } = true;
    public bool HideInFullscreen { get; init; } = true;
    public bool Notifications { get; init; } = true;
    public bool ShowMedia { get; init; } = true;
    public bool ReducedMotion { get; init; }
    public bool StartWithWindows { get; init; }
    public AppRule[] Rules { get; init; } = [new("devenv.exe", Profile.Work), new("Code.exe", Profile.Work)];
    public QuickAction[] QuickActions { get; init; } = [new("Focus", ActionKind.Profile, "Focus"), new("Work", ActionKind.Profile, "Work")];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }

    public PulseSettings Normalize() => this with
    {
        Theme = Theme is "Dark" or "Light" or "System" or "Akeno" ? Theme : "System",
        AkenoBackground = AkenoBackground is "Samurai" or "Gradient" or "None" ? AkenoBackground : "Samurai",
        Workspaces = ProfileWorkspaces.Normalize(Workspaces),
        UserName = new string((UserName ?? "").Where(c => !char.IsControl(c)).Take(32).ToArray()).Trim(),
        WeatherEnabled = WeatherEnabled && WeatherLocation is { IsValid: true },
        WeatherLocation = WeatherLocation is { IsValid: true } city ? city : null,
        WeatherUnit = WeatherUnit == "Fahrenheit" ? "Fahrenheit" : "Celsius",
        PollingIntervalMs = Math.Clamp(PollingIntervalMs, 500, 10000),
        ObsPort = Math.Clamp(ObsPort, 1024, 65535),
        ObsScenes = Enumerable.Range(0, 3).Select(i => i < (ObsScenes?.Length ?? 0) ? (ObsScenes![i] ?? "").Trim() : "").ToArray(),
        PreferredMusicPlayer = MusicPlayers.Names.Contains(PreferredMusicPlayer) ? PreferredMusicPlayer : "All",
        Position = Position is { } p ? p with
        {
            Monitor = p.Monitor ?? "",
            Orientation = p.Orientation == "Horizontal" ? "Horizontal" : "Vertical",
            Anchor = p.Orientation == "Horizontal"
                ? p.Anchor is "Top edge" or "Top center" ? "Top edge" : "Bottom edge"
                : p.Anchor is "Left edge" or "Bottom left" ? "Left edge" : "Right edge",
            OffsetX = double.IsFinite(p.OffsetX) ? Math.Clamp(p.OffsetX, -100000, 100000) : 0,
            OffsetY = double.IsFinite(p.OffsetY) ? Math.Clamp(p.OffsetY, -100000, 100000) : 0
        } : new(),
        Hotkey = Hotkey is { Key: >= 0x20 and <= 0xFE, Modifiers: > 0 and <= 15 } ? Hotkey : new(),
        HideHotkey = HideHotkey is { Key: >= 0x20 and <= 0xFE, Modifiers: > 0 and <= 15 } ? HideHotkey : new(3, 0x48),
        SearchHotkey = SearchHotkey is { Key: >= 0x20 and <= 0xFE, Modifiers: > 0 and <= 15 } ? SearchHotkey : new(1, 0x20),
        SearchFolders = (SearchFolders ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToArray(),
        DefaultProfile = Enum.IsDefined(DefaultProfile) ? DefaultProfile : Profile.Default,
        LockedProfile = LockedProfile.HasValue && Enum.IsDefined(LockedProfile.Value) ? LockedProfile : null,
        Rules = (Rules ?? []).Where(r => r is not null && !string.IsNullOrWhiteSpace(r.Identity) && Enum.IsDefined(r.Profile))
            .Take(200).Select(r => r with { Identity = r.Identity.Trim(), Priority = Math.Clamp(r.Priority, 0, 10000) }).ToArray(),
        QuickActions = (QuickActions ?? []).Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Label) && Enum.IsDefined(a.Kind))
            .Take(24).Select(a => a with { Label = a.Label.Trim(), Target = a.Target ?? "" }).ToArray()
    };
}

public sealed record SystemSnapshot(double? Cpu, ulong TotalMemory, ulong AvailableMemory, double? ReceiveBytesPerSecond, double? SendBytesPerSecond, string? Error = null, PowerSnapshot? Power = null)
{
    public double? MemoryPercent => TotalMemory == 0 ? null : 100d * (TotalMemory - AvailableMemory) / TotalMemory;
}
public sealed record ForegroundApplication(int ProcessId, string Name, string? ExecutablePath, bool IsFullscreen = false)
{
    public static ForegroundApplication Empty { get; } = new(0, "Desktop", null);
}
public sealed record MediaSnapshot(string Title, string Artist, bool Playing, bool Available, bool CanPlayPause = false,
    bool CanPrevious = false, bool CanNext = false, byte[]? Artwork = null, string? Error = null)
{
    public static bool IsDetected(string? title, string? artist, bool playing) => playing || !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(artist);
    public static MediaSnapshot Empty { get; } = new("Nothing playing", "Start playback in a media app", false, false);
}
public sealed record AudioSnapshot(double? Volume, bool Muted, string Device, string? Error = null, double? Peak = null);
public sealed record ActivityEntry(DateTimeOffset Time, string Message);
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
