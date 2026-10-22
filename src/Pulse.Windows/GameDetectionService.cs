using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Pulse.Core;

namespace Pulse.Windows;

// Read launcher metadata only. No recursive disk scans and no FPS/fullscreen heuristics.
public sealed class GameDetectionService(Func<IEnumerable<GameLibrary>>? discover = null)
{
    private readonly Func<IEnumerable<GameLibrary>> _discover = discover ?? Discover;
    private readonly object _gate = new();
    private GameLibrary[] _libraries = [];
    private long _refreshAfter;
    public int LibraryCount { get { lock (_gate) return _libraries.Length; } }
    public void Invalidate() { lock (_gate) _refreshAfter = 0; }

    public ForegroundApplication Classify(ForegroundApplication application)
    {
        lock (_gate)
        {
            if (Environment.TickCount64 >= _refreshAfter)
            {
                _libraries = _discover().DistinctBy(x => x.Directory, StringComparer.OrdinalIgnoreCase).Take(256).ToArray();
                _refreshAfter = Environment.TickCount64 + 60000;
            }
            var source = GameLibraries.SourceFor(application.ExecutablePath, _libraries);
            return application with { IsGame = source is not null, GameSource = source };
        }
    }

    private static IEnumerable<GameLibrary> Discover()
    {
        var libraries = new List<GameLibrary>();
        void Add(string? path, string source) { if (!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && Directory.Exists(path)) libraries.Add(new(path, source)); }
        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steam = steamKey?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steam))
            {
                Add(Path.Combine(steam, "steamapps", "common"), "Steam");
                var metadata = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(metadata) && new FileInfo(metadata).Length < 1024 * 1024)
                    foreach (Match match in Regex.Matches(File.ReadAllText(metadata), "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
                        Add(Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "steamapps", "common"), "Steam");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or RegexMatchTimeoutException) { }
        try
        {
            var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(manifests)) foreach (var file in Directory.EnumerateFiles(manifests, "*.item").Take(256))
            {
                try
                {
                    if (new FileInfo(file).Length > 1024 * 1024) continue;
                    using var item = JsonDocument.Parse(File.ReadAllText(file));
                    if (item.RootElement.TryGetProperty("InstallLocation", out var path) && path.ValueKind == JsonValueKind.String)
                        Add(path.GetString(), "Epic Games");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        foreach (var keyName in new[] { @"SOFTWARE\GOG.com\Games", @"SOFTWARE\WOW6432Node\GOG.com\Games" })
        {
            try
            {
                using var games = root.OpenSubKey(keyName);
                foreach (var key in games?.GetSubKeyNames().Take(256) ?? [])
                { using var game = games!.OpenSubKey(key); Add(game?.GetValue("path") as string, "GOG"); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
        }
        return libraries;
    }
}
