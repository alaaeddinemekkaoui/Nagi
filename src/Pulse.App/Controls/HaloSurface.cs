using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Pulse.App.Controls;

// Paint the complete client surface; native clipping owns the outer silhouette.
public sealed class HaloSurface : UserControl
{
    private readonly CanvasControl _canvas = new() { ClearColor = Microsoft.UI.Colors.Transparent };
    private Color _color = Color.FromArgb(220, 16, 11, 13);
    private string _edge = "Right edge";
    private double _drop, _notch;
    public HaloSurface()
    {
        IsHitTestVisible = false; Content = _canvas;
        _canvas.Draw += (_, args) =>
        {
            var width = (float)_canvas.ActualWidth; var height = (float)_canvas.ActualHeight;
            if (width <= 0 || height <= 0 || _color.A == 0) return;
            using var geometry = Geometry(width, height);
            args.DrawingSession.Antialiasing = Microsoft.Graphics.Canvas.CanvasAntialiasing.Antialiased;
            args.DrawingSession.FillGeometry(geometry, _color);
        };
        SizeChanged += (_, _) => _canvas.Invalidate();
    }
    public void SetColor(Color color)
    { if (_color.Equals(color)) return; _color = color; _canvas.Invalidate(); }
    public void SetShape(double drop, string edge, double notch = 0)
    { if (Math.Abs(_drop - drop) < .00001 && Math.Abs(_notch - notch) < .00001 && _edge == edge) return; _drop = drop; _notch = notch; _edge = edge; _canvas.Invalidate(); }
    private CanvasGeometry Geometry(float width, float height)
    {
        if (_drop <= .001) return CanvasGeometry.CreateRectangle(_canvas, 0, 0, width, height);
        var points = Pulse.Core.Motion.RestingContour.BezierPoints(width, height, _edge, _drop, _notch);
        using var path = new CanvasPathBuilder(_canvas);
        var horizontal = _edge is "Top edge" or "Bottom edge";
        var attachedStart = _edge switch
        {
            "Right edge" => new System.Numerics.Vector2(width, 0),
            "Bottom edge" => new System.Numerics.Vector2(0, height),
            _ => System.Numerics.Vector2.Zero
        };
        var attachedEnd = horizontal ? new System.Numerics.Vector2(width, attachedStart.Y)
            : new System.Numerics.Vector2(attachedStart.X, height);
        path.BeginFigure(attachedStart);
        path.AddLine(horizontal ? new(0, points[0].Y) : new(points[0].X, 0));
        path.AddLine(points[0]);
        for (var i = 0; i + 3 < points.Length; i += 3) path.AddCubicBezier(points[i + 1], points[i + 2], points[i + 3]);
        path.AddLine(horizontal ? new(width, points[^1].Y) : new(points[^1].X, height));
        path.AddLine(attachedEnd);
        path.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(path);
    }
}
