namespace Pulse.Core;

public sealed record ProfileApp(string Name, string Target, string Kind = "Catalog");

public sealed record ProfileWorkspace(Profile Profile, ProfileApp[] Apps, string[] Widgets, bool AutoOpenApps = false)
{
    public string ObsPresentation { get; init; } = "Widget";
    public bool Shows(string widget) => Widgets.Contains(widget, StringComparer.Ordinal);
    public ProfileWorkspace Normalize() => this with
    {
        ObsPresentation = ObsPresentation is "App" or "Hidden" ? ObsPresentation : "Widget",
        Apps = (Apps ?? []).Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Name)
                && !string.IsNullOrWhiteSpace(a.Target) && a.Name.Length <= 80 && a.Target.Length <= 2048
                && (a.Kind == "Catalog" || a.Kind == "Suggested" && ProfileWorkspaces.Suggestions.ContainsKey(a.Target)))
            .DistinctBy(a => (a.Kind, a.Target)).Take(16).ToArray(),
        Widgets = (Widgets ?? []).Where(ProfileWorkspaces.WidgetNames.Contains).Distinct().ToArray()
    };
}

public static class ProfileWorkspaces
{
    public static bool IsObsApp(ProfileApp app) => app.Target.Equals("obs", StringComparison.OrdinalIgnoreCase)
        || app.Name.StartsWith("OBS Studio", StringComparison.OrdinalIgnoreCase)
        || Path.GetFileNameWithoutExtension(app.Target) is "obs64" or "obs32";
    // The studio slot owns OBS in this profile; avoid showing both an app and a widget.
    public static ProfileApp[] DockApps(ProfileWorkspace workspace) => workspace.Shows("Studio") || workspace.ObsPresentation == "Hidden"
        ? workspace.Apps.Where(app => !IsObsApp(app)).ToArray() : workspace.Apps;
    public static bool ShowObs(ProfileWorkspace workspace) => workspace.Shows("Studio") && workspace.ObsPresentation != "Hidden";
    public static readonly Profile[] Profiles = [Profile.Default, Profile.Work, Profile.Gaming, Profile.Stream, Profile.Focus];
    public static readonly string[] WidgetNames = ["Performance", "Network", "Media", "Audio", "Microphone", "Weather", "Studio", "Battery"];
    public static readonly IReadOnlyDictionary<string, string[]> Suggestions = new Dictionary<string, string[]>
    {
        ["code"] = ["Visual Studio Code", "VS Code"], ["terminal"] = ["Windows Terminal", "Terminal"],
        ["browser"] = ["Google Chrome", "Microsoft Edge", "Firefox", "Brave"], ["steam"] = ["Steam"], ["obs"] = ["OBS Studio", "OBS Studio (64bit)"]
    };
    public static ProfileWorkspace Default(Profile profile) => new(profile, profile switch
    {
        Profile.Work => [new("VS Code", "code", "Suggested"), new("Terminal", "terminal", "Suggested"), new("Browser", "browser", "Suggested")],
        Profile.Gaming => [new("Steam", "steam", "Suggested"), new("Browser", "browser", "Suggested")],
        Profile.Default => [],
        Profile.Stream => [new("OBS Studio", "obs", "Suggested")],
        _ => [new("Browser", "browser", "Suggested")]
    }, profile switch
    {
        Profile.Default => ["Performance", "Media"],
        Profile.Focus => ["Media", "Audio", "Microphone", "Weather", "Battery"],
        Profile.Stream => ["Performance", "Media", "Audio", "Studio", "Microphone", "Weather", "Battery"],
        _ => ["Performance", "Media", "Audio", "Microphone", "Weather", "Battery"]
    });
    public static ProfileWorkspace[] Normalize(ProfileWorkspace[]? workspaces) => Profiles
        .Select(profile => (workspaces?.FirstOrDefault(w => w is not null && w.Profile == profile) ?? Default(profile)).Normalize()).ToArray();
    public static SearchEntry? Resolve(ProfileApp app, IEnumerable<SearchEntry> catalog)
    {
        var apps = catalog.Where(e => e.Kind == SearchKind.Application).ToArray();
        if (app.Kind == "Catalog") return apps.FirstOrDefault(e => string.Equals(e.Target, app.Target, StringComparison.OrdinalIgnoreCase));
        if (app.Kind != "Suggested" || !Suggestions.TryGetValue(app.Target, out var names)) return null;
        return names.Select(name => apps.FirstOrDefault(e => string.Equals(e.Title, name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(e => e is not null);
    }
    // Only an explicit profile activation can launch apps. Background detection and repeated UI refreshes cannot.
    public static bool ShouldOpenApps(Profile? activated, ProfileWorkspace workspace, bool explicitActivation, bool alreadyOpening)
        => explicitActivation && activated == workspace.Profile && workspace.AutoOpenApps && !alreadyOpening;
}
