using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Pulse.App.Controls;

// Vector-only progress; updates once per timer tick, without a rendering loop.
public sealed class TimerRing : Grid
{
    private readonly Ellipse _track = new() { StrokeThickness = 2, Margin = new(4) };
    private readonly Path _progress = new() { StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private double _remaining = 1;

    public TimerRing()
    {
        IsHitTestVisible = false;
        Children.Add(_track); Children.Add(_progress);
        SizeChanged += (_, _) => Draw();
    }

    public void Update(double remaining, Brush accent, Brush track)
    {
        _track.Stroke = track; _track.Opacity = .16;
        _progress.Stroke = accent;
        remaining = Math.Clamp(remaining, 0, 1);
        if (Math.Abs(remaining - _remaining) < .00001 && _progress.Data is not null) return;
        _remaining = remaining; Draw();
    }

    private void Draw()
    {
        var side = Math.Min(ActualWidth, ActualHeight);
        if (side < 8) return;
        var center = side / 2; var radius = center - 4;
        var fraction = Math.Min(_remaining, .99999);
        var angle = fraction * Math.PI * 2 - Math.PI / 2;
        var figure = new PathFigure { StartPoint = new(center, center - radius), IsFilled = false };
        figure.Segments.Add(new ArcSegment { Point = new(center + Math.Cos(angle) * radius, center + Math.Sin(angle) * radius),
            Size = new Size(radius, radius), SweepDirection = SweepDirection.Clockwise, IsLargeArc = fraction > .5 });
        var geometry = new PathGeometry(); geometry.Figures.Add(figure); _progress.Data = geometry;
        _progress.Visibility = _remaining > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
