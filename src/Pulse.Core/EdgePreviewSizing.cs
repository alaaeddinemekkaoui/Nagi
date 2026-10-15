namespace Pulse.Core;

/// <summary>Sizes an edge preview to the space above/below its unchanged compact strip.</summary>
public static class EdgePreviewSizing
{
    public static (bool Above, double Height) Fit(double top, double bottom, double center, double scale, double requested)
    {
        var above = center > (top + bottom) / 2;
        var room = above ? center + 42 * scale - top - 8 : bottom - (center - 42 * scale) - 8;
        return (above, Math.Clamp(requested * scale, Math.Min(84 * scale, Math.Max(16, room)), Math.Max(16, room)));
    }
}
