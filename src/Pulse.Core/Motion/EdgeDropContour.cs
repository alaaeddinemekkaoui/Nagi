using System.Numerics;

namespace Pulse.Core.Motion;

public static class EdgeDropContour
{
    // One flat side meets the display; the free side eases into a liquid bulge.
    public static Vector2[] Create(float width, float height, string edge, double amount = 1)
    {
        const int segments = 64;
        var horizontal = edge is "Top edge" or "Bottom edge";
        var points = new Vector2[segments + 3];
        var depth = horizontal ? height : width; var length = horizontal ? width : height;
        Vector2 Map(float along, float inward) => edge switch
        {
            "Left edge" => new(inward, along), "Top edge" => new(along, inward),
            "Bottom edge" => new(along, height - inward), _ => new(width - inward, along)
        };
        points[0] = Map(0, 0);
        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var bulge = Math.Pow(Math.Sin(Math.PI * t), 2);
            points[i + 1] = Map(t * length, (float)(depth * (1 - Math.Clamp(amount, 0, 1) * (1 - bulge))));
        }
        points[^1] = Map(length, 0);
        return points;
    }
}
