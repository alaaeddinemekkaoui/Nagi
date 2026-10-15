using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Pulse.App.Controls;

// Win2D/Direct2D owns the antialiased surface. No polygon window region or outline stroke.
public sealed class HaloSurface : UserControl
{
    private readonly CanvasControl _canvas = new() { ClearColor = Microsoft.UI.Colors.Transparent };
    private Color _color = Color.FromArgb(220, 16, 11, 13);
    public HaloSurface()
    {
        IsHitTestVisible = false; Content = _canvas;
        _canvas.Draw += (_, args) =>
        {
            var width = (float)_canvas.ActualWidth; var height = (float)_canvas.ActualHeight;
            if (width <= 0 || height <= 0) return;
            var radius = Math.Min(22, Math.Min(width, height) / 2);
            using var geometry = CanvasGeometry.CreateRoundedRectangle(_canvas, 0, 0, width, height, radius, radius);
            args.DrawingSession.FillGeometry(geometry, _color);
        };
        SizeChanged += (_, _) => _canvas.Invalidate();
    }
    public void SetColor(Color color) { _color = color; _canvas.Invalidate(); }
}
