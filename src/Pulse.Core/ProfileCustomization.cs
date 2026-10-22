namespace Pulse.Core;

public static class ProfileCustomization
{
    public static PulseSettings Create(PulseSettings settings, string name, Profile template)
    {
        name = ValidateName(settings, name);
        if (settings.CustomProfiles.Length >= 16) throw new ArgumentException("You can create up to 16 custom profiles.");
        var id = (Profile)Enumerable.Range(1000, 1000).First(i => settings.CustomProfiles.All(p => (int)p.Id != i));
        return (settings with { CustomProfiles = [.. settings.CustomProfiles, new(id, name)],
            Workspaces = [.. settings.Workspaces, settings.Workspace(template) with { Profile = id, AutoOpenApps = false, Mode = ModePresets.For(settings.Workspace(template)), Timer = settings.TimerFor(template) }] }).Normalize();
    }
    public static PulseSettings Rename(PulseSettings settings, Profile id, string name)
    {
        if (settings.CustomProfiles.All(p => p.Id != id)) throw new ArgumentException("Choose a custom profile.");
        name = ValidateName(settings, name, id);
        return settings with { CustomProfiles = settings.CustomProfiles.Select(p => p.Id == id ? p with { Name = name } : p).ToArray() };
    }
    public static PulseSettings Remove(PulseSettings settings, Profile id)
    {
        if (settings.CustomProfiles.All(p => p.Id != id)) return settings;
        return (settings with { CustomProfiles = settings.CustomProfiles.Where(p => p.Id != id).ToArray(),
            Workspaces = settings.Workspaces.Where(w => w.Profile != id).ToArray(), Rules = settings.Rules.Where(r => r.Profile != id).ToArray(),
            QuickActions = settings.QuickActions.Where(a => a.Profile != id && !(a.Kind == ActionKind.Profile && a.Target == ((int)id).ToString())).ToArray(),
            LockedProfile = settings.LockedProfile == id ? null : settings.LockedProfile,
            DefaultProfile = settings.DefaultProfile == id ? Profile.Default : settings.DefaultProfile,
            TimerProfile = settings.TimerProfile == id ? Profile.Focus : settings.TimerProfile }).Normalize();
    }
    private static string ValidateName(PulseSettings settings, string name, Profile? except = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl)) throw new ArgumentException("Enter a profile name of 1–40 characters.");
        if (name.Equals("Automatic", StringComparison.OrdinalIgnoreCase) || name.Equals("All profiles", StringComparison.OrdinalIgnoreCase)
            || settings.Profiles.Any(p => p != except && settings.ProfileName(p).Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A profile with this name already exists.");
        return name;
    }
}
