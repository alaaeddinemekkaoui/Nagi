using System.Numerics;

namespace Pulse.Core.Motion;

// One silhouette for painting, acrylic masking and native hit testing.
public static class RestingContour
{
    public static Vector2[] BezierPoints(float width, float height, string edge, double drop = 1, double notch = 0, float rasterizationScale = 1)
    {
        var original = EdgeDropContour.BezierPoints(width, height, edge, drop);
        if (notch <= 0) return original;
        var idle = Subdivide(original);
        var active = Notch(width, height, edge, Math.Max(1, rasterizationScale));
        var horizontal = edge is "Top edge" or "Bottom edge";
        var attached = edge switch { "Bottom edge" => height, "Right edge" => width, _ => 0f };
        var free = horizontal ? height - attached : width - attached;
        for (var i = 0; i < active.Length; i++)
        {
            // Drop zero is the full panel, so a hover morph stays continuous.
            if (horizontal) active[i].Y = float.Lerp(free, active[i].Y, (float)Math.Clamp(drop, 0, 1));
            else active[i].X = float.Lerp(free, active[i].X, (float)Math.Clamp(drop, 0, 1));
            active[i] = Vector2.Lerp(idle[i], active[i], (float)Math.Clamp(notch, 0, 1));
        }
        return active;
    }
    public static Vector2[] Create(float width, float height, string edge, double drop = 1, double notch = 0, float rasterizationScale = 1)
    {
        if (notch <= 0) return EdgeDropContour.Create(width, height, edge, drop);
        var curve = BezierPoints(width, height, edge, drop, notch, rasterizationScale);
        var horizontal = edge is "Top edge" or "Bottom edge";
        var start = edge switch { "Right edge" => new Vector2(width, 0), "Bottom edge" => new Vector2(0, height), _ => Vector2.Zero };
        var points = new List<Vector2> { start, horizontal ? new(0, curve[0].Y) : new(curve[0].X, 0), curve[0] };
        var segments = Math.Clamp((int)Math.Ceiling(Math.Max(width, height) / 12), 12, 128);
        for (var i = 0; i + 3 < curve.Length; i += 3)
            for (var step = 1; step <= segments; step++)
                points.Add(At(curve, i, (float)step / segments));
        points.Add(horizontal ? new(width, curve[^1].Y) : new(curve[^1].X, height));
        points.Add(horizontal ? new(width, start.Y) : new(start.X, height)); points.Add(start);
        return points.ToArray();
    }
    private static Vector2[] Notch(float width, float height, string edge, float scale)
    {
        Vector2[] points;
        if (edge is "Top edge" or "Bottom edge")
        {
            var mid = width / 2;
            var shoulder = Math.Min(32 * scale, width / 3);
            // Gentle shoulders leave a small flat bed sized for the actual content.
            points = [new(0, 0), new(shoulder * .22f, 0), new(shoulder * .3f, height * .18f), new(shoulder * .5f, height * .45f),
                new(shoulder * .7f, height * .72f), new(shoulder * .78f, height), new(shoulder, height),
                new(shoulder * 1.22f, height), new(mid - shoulder * .22f, height), new(mid, height),
                new(mid + shoulder * .22f, height), new(width - shoulder * 1.22f, height), new(width - shoulder, height),
                new(width - shoulder * .78f, height), new(width - shoulder * .7f, height * .72f), new(width - shoulder * .5f, height * .45f),
                new(width - shoulder * .3f, height * .18f), new(width - shoulder * .22f, 0), new(width, 0)];
            if (edge == "Bottom edge") for (var i = 0; i < points.Length; i++) points[i].Y = height - points[i].Y;
        }
        else
        {
            points = Notch(height, width, "Top edge", scale);
            for (var i = 0; i < points.Length; i++) points[i] = new(points[i].Y, points[i].X);
            if (edge == "Right edge") for (var i = 0; i < points.Length; i++) points[i].X = width - points[i].X;
        }
        return points;
    }
    private static Vector2[] Subdivide(Vector2[] points)
    {
        var result = new List<Vector2> { points[0] };
        for (var side = 0; side < 2; side++)
            for (var part = 0; part < 3; part++)
            {
                var a = part / 3f; var b = (part + 1) / 3f; var offset = side * 3;
                result.Add(At(points, offset, a) + Derivative(points, offset, a) / 9);
                result.Add(At(points, offset, b) - Derivative(points, offset, b) / 9);
                result.Add(At(points, offset, b));
            }
        return result.ToArray();
    }
    private static Vector2 At(Vector2[] p, int i, float t)
    { var u = 1 - t; return u * u * u * p[i] + 3 * u * u * t * p[i + 1] + 3 * u * t * t * p[i + 2] + t * t * t * p[i + 3]; }
    private static Vector2 Derivative(Vector2[] p, int i, float t)
    { var u = 1 - t; return 3 * u * u * (p[i + 1] - p[i]) + 6 * u * t * (p[i + 2] - p[i + 1]) + 3 * t * t * (p[i + 3] - p[i + 2]); }
}
