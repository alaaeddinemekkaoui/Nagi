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
    private double _drop;
    public HaloSurface()
    {
        IsHitTestVisible = false; Content = _canvas;
        _canvas.Draw += (_, args) =>
        {
            var width = (float)_canvas.ActualWidth; var height = (float)_canvas.ActualHeight;
            if (width <= 0 || height <= 0) return;
            var radius = Math.Min(22, Math.Min(width, height) / 2);
            using var geometry = Geometry(width, height, radius);
            args.DrawingSession.FillGeometry(geometry, _color);
        };
        SizeChanged += (_, _) => _canvas.Invalidate();
    }
    public void SetColor(Color color) { _color = color; _canvas.Invalidate(); }
    public void SetShape(double drop, string edge) { _drop = drop; _edge = edge; _canvas.Invalidate(); }
    private CanvasGeometry Geometry(float width, float height, float radius)
    {
        if (_drop <= .001) return CanvasGeometry.CreateRectangle(_canvas, 0, 0, width, height);
        var points = Pulse.Core.Motion.EdgeDropContour.Create(width, height, _edge, _drop);
        using var path = new CanvasPathBuilder(_canvas);
        path.BeginFigure(points[0]);
        foreach (var point in points.Skip(1)) path.AddLine(point);
        path.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(path);
    }
}
