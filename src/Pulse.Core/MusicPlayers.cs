namespace Pulse.Core;

public sealed record MusicPlayer(string Name, string Url);

public static class MusicPlayers
{
    public static readonly MusicPlayer[] Services =
    [
        new("YouTube Music", "https://music.youtube.com/"),
        new("Spotify", "https://open.spotify.com/"),
        new("Apple Music", "https://music.apple.com/")
    ];
    public static readonly string[] Names = ["All", .. Services.Select(s => s.Name)];
    public static IEnumerable<MusicPlayer> For(string preference) =>
        preference == "All" || !Names.Contains(preference) ? Services : Services.Where(s => s.Name == preference);
}
