using System.Numerics;

namespace Pulse.Core.Motion;

public static class EdgeDropContour
{
    // Two mirrored cubics share horizontal tangents at the shoulders and center.
    // Coordinates are relative to the current client bounds, including during motion.
    public static Vector2[] BezierPoints(float width, float height, string edge, double amount = 1)
    {
        var horizontal = edge is "Top edge" or "Bottom edge";
        var depth = horizontal ? height : width;
        var length = horizontal ? width : height;
        // At rest both ends meet the screen itself: no horizontal shoulder strip.
        var shoulder = (float)(depth * (1 - Math.Clamp(amount, 0, 1)));
        return [Map(width, height, edge, length * .18f, shoulder),
            Map(width, height, edge, length * .29f, shoulder),
            Map(width, height, edge, length * .35f, depth),
            Map(width, height, edge, length * .5f, depth),
            Map(width, height, edge, length * .65f, depth),
            Map(width, height, edge, length * .71f, shoulder),
            Map(width, height, edge, length * .82f, shoulder)];
    }

    // Flatten the same cubics for the HWND region and its pointer hit testing.
    // Subpixel sampling avoids a fixed low-resolution polygon at large DPI/widths.
    public static Vector2[] Create(float width, float height, string edge, double amount = 1)
    {
        var horizontal = edge is "Top edge" or "Bottom edge";
        var length = horizontal ? width : height;
        var segments = Math.Clamp((int)Math.Ceiling(length / 4), 32, 512);
        var curve = BezierPoints(width, height, edge, amount);
        var points = new Vector2[segments * 2 + 6];
        points[0] = Map(width, height, edge, 0, 0);
        var shoulder = horizontal ? Math.Abs(curve[0].Y - points[0].Y) : Math.Abs(curve[0].X - points[0].X);
        points[1] = Map(width, height, edge, 0, shoulder);
        points[2] = curve[0];
        for (var side = 0; side < 2; side++)
        {
            var offset = side * 3;
            for (var i = 1; i <= segments; i++)
            {
                var t = (float)i / segments; var u = 1 - t;
                points[3 + side * segments + i - 1] = u * u * u * curve[offset]
                    + 3 * u * u * t * curve[offset + 1] + 3 * u * t * t * curve[offset + 2] + t * t * t * curve[offset + 3];
            }
        }
        points[^3] = Map(width, height, edge, length, shoulder);
        points[^2] = Map(width, height, edge, length, 0);
        points[^1] = points[0];
        return points;
    }

    private static Vector2 Map(float width, float height, string edge, float along, float inward) => edge switch
    {
        "Left edge" => new(inward, along), "Top edge" => new(along, inward),
        "Bottom edge" => new(along, height - inward), _ => new(width - inward, along)
    };
}
