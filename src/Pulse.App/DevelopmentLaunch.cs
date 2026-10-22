using Pulse.Core;

namespace Pulse.App;

internal sealed record DevelopmentLaunch(string Page, bool RunChecks, string[] Arguments)
{
    internal static DevelopmentLaunch Parse(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray(); var standard = new List<string>();
        var page = "Developer tools"; var checks = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dev-checks") checks = true;
            else if (args[i] == "--dev-page")
            {
                if (++i >= args.Length || !DevelopmentPages.All.Contains(args[i], StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("Choose a valid Nagi page for --dev-page.");
                page = DevelopmentPages.All.First(p => p.Equals(args[i], StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                standard.Add(args[i]);
                if (args[i] is "--data-dir" or "--wait-for-exit" && i + 1 < args.Length) standard.Add(args[++i]);
            }
        }
        return new(page, checks, standard.ToArray());
    }
    internal static NagiLaunchOptions Isolate(NagiLaunchOptions options) => options with
    {
        DataDirectory = options.DataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nagi", "Development")
    };
}

internal static class DevelopmentPages
{
    internal static readonly string[] Settings = ["General", "Language", "Appearance & dock", "Profiles & apps", "Weather",
        "Hardware & audio", "Integrations", "Search & shortcuts", "Lua UI studio", "Music", "Timer & live activity", "Updates", "Behavior", "About & diagnostics", "Developer tools"];
    internal static readonly string[] All = ["Overview", "Profiles", "Utilities", "Settings", .. Settings];
}
