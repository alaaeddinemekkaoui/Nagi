namespace Pulse.Core;

public sealed class ContextEngine : IContextEngine
{
    private Profile? _candidate;
    private DateTimeOffset _candidateSince;
    public Profile Current { get; private set; } = Profile.Default;
    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(1800);

    public Profile Evaluate(ForegroundApplication application, PulseSettings settings, DateTimeOffset now)
    {
        if (!ProfileWorkspaces.IsKnown(Current, settings.CustomProfiles)) Current = settings.DefaultProfile;
        if (settings.LockedProfile is { } locked)
        {
            _candidate = null;
            return Current = locked;
        }
        if (!settings.AutomaticSwitching)
        {
            _candidate = null;
            return Current = settings.DefaultProfile;
        }
        // A Pulse window is deliberately ignored by the coordinator, preserving the user's context.
        var desired = settings.Rules.Where(r => r.Enabled && Matches(r.Identity, application))
            .OrderByDescending(r => r.Priority).Select(r => (Profile?)r.Profile).FirstOrDefault()
            ?? (settings.AutomaticGameDetection && application.IsGame ? Profile.Gaming : settings.DefaultProfile);
        if (desired == Current) { _candidate = null; return Current; }
        if (_candidate != desired) { _candidate = desired; _candidateSince = now; return Current; }
        if (now - _candidateSince >= Debounce) { Current = desired; _candidate = null; }
        return Current;
    }

    public static bool Matches(string identity, ForegroundApplication app)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;
        identity = identity.Trim();
        if (identity.Contains('\\') || identity.Contains('/'))
            return string.Equals(identity, app.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        var file = app.ExecutablePath?.Replace('\\', '/').Split('/').LastOrDefault() ?? app.Name;
        return string.Equals(identity, file, StringComparison.OrdinalIgnoreCase)
            || string.Equals(identity, app.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(identity, app.Name + ".exe", StringComparison.OrdinalIgnoreCase);
    }
}
