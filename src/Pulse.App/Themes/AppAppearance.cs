using Pulse.Core;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Pulse.App.Themes;

internal static class AppAppearance
{
    private static readonly UISettings SystemColors = new();
    public static bool UsesGlass(PulseSettings settings) => settings.Theme == "System" || settings.GlassEnabled;

    public static Color? Tint(PulseSettings settings) => settings.Theme is "Dark" or "Light" ? settings.NotchColorMode switch
    {
        "System" => SystemColors.GetColorValue(UIColorType.Accent),
        "Custom" => Color.FromArgb(255, Convert.ToByte(settings.NotchColor.Substring(1, 2), 16),
            Convert.ToByte(settings.NotchColor.Substring(3, 2), 16), Convert.ToByte(settings.NotchColor.Substring(5, 2), 16)),
        _ => null
    } : null;

    public static Color Surface(PulseSettings settings, bool light, bool glass)
    {
        var color = HaloAcrylicBackdrop.SurfaceColor(light, settings.Theme == "Akeno", glass);
        if (Tint(settings) is { } tint)
        {
            color.R = (byte)(color.R * .84 + tint.R * .16);
            color.G = (byte)(color.G * .84 + tint.G * .16);
            color.B = (byte)(color.B * .84 + tint.B * .16);
        }
        return color;
    }
}
