namespace Pulse.Core;

public static class DockPresentation
{
    public const double HorizontalHeight = 56;
    public const double VerticalWidth = 64;
    public static bool ShowBattery(ProfileWorkspace workspace, PowerSnapshot? power) => workspace.Shows("Battery") && power?.HasBattery == true;
    public static bool GroupApps(int count) => count > 2;
    public static string ShortTitle(string? title, int maxCharacters = 24)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var words = title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var shortText = string.Join(" ", words.Take(3));
        var characters = System.Globalization.StringInfo.ParseCombiningCharacters(shortText);
        var clipped = characters.Length > Math.Max(1, maxCharacters);
        if (clipped) shortText = shortText[..characters[Math.Max(1, maxCharacters)]].TrimEnd();
        return clipped || words.Length > 3 ? shortText + "\u2026" : shortText;
    }
    public static double RailExtent(ProfileWorkspace workspace, bool horizontal, bool showMedia, bool weatherEnabled, PowerSnapshot? power)
    {
        var extents = new List<double> { 32, 32 }; // Logo, search, then applications.
        var apps = GroupApps(workspace.Apps.Length) ? 1 : workspace.Apps.Length;
        extents.AddRange(Enumerable.Repeat(horizontal ? 34d : 32d, apps));
        if (workspace.Shows("Performance")) { extents.Add(horizontal ? 56 : 28); extents.Add(horizontal ? 56 : 28); }
        if (workspace.Shows("Media") && showMedia || workspace.Shows("Audio")) extents.Add(horizontal ? 168 : 50);
        if (workspace.Shows("Studio")) extents.Add(32);
        if (workspace.Shows("Microphone")) extents.Add(30);
        if (workspace.Shows("Weather") && weatherEnabled) extents.Add(horizontal ? 44 : 30);
        if (ShowBattery(workspace, power)) extents.Add(horizontal ? 44 : 30);
        return extents.Sum() + extents.Count * 4 + 16;
    }
}
