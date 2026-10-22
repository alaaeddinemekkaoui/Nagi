namespace Pulse.Core;

public sealed record GameLibrary(string Directory, string Source);

public static class GameLibraries
{
    private static readonly HashSet<string> NonGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam.exe", "steamwebhelper.exe", "epicgameslauncher.exe", "epicwebhelper.exe", "galaxyclient.exe",
        "goggalaxy.exe", "riotclientservices.exe", "battle.net.exe", "eadesktop.exe", "ubisoftconnect.exe",
        "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "obs64.exe", "obs32.exe",
        "vlc.exe", "mpv.exe", "potplayer64.exe", "wallpaper64.exe", "wallpaper32.exe",
        "crashpad_handler.exe", "crashreportclient.exe", "unitycrashhandler64.exe", "unrealeditor.exe", "ue4editor.exe", "easyanticheat.exe",
        "easyanticheat_eos.exe", "beservice.exe", "unins000.exe", "uninstall.exe", "setup.exe", "nagi.exe"
    };

    public static string? SourceFor(string? executablePath, IEnumerable<GameLibrary> libraries)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;
        var path = executablePath.Replace('/', '\\');
        var name = path.Split('\\').Last();
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || NonGames.Contains(name)
            || name.Contains("crashreport", StringComparison.OrdinalIgnoreCase)) return null;
        return libraries.FirstOrDefault(library =>
        {
            var root = library.Directory.Replace('/', '\\').TrimEnd('\\');
            return root.Length >= 3 && path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
        })?.Source;
    }
}
