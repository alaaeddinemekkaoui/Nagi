using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Pulse.App.Controls;

// Animate geometry instead of scaling rectangles, which flattens rounded ends.
public sealed class MusicVisualizer : UserControl
{
    private readonly Grid _host = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly double[] _history;
    private readonly CompositionRoundedRectangleGeometry?[] _bars;
    private readonly CompositionColorBrush?[] _brushes;
    private readonly Vector2KeyFrameAnimation?[] _sizes, _offsets;
    private ShapeVisual? _visual;
    private Color _color;
    private bool? _available;
    public MusicVisualizer() : this(24) { }
    public MusicVisualizer(int bars)
    {
        bars = Math.Clamp(bars, 3, 24);
        _history = new double[bars]; _bars = new CompositionRoundedRectangleGeometry?[bars];
        _brushes = new CompositionColorBrush?[bars]; _sizes = new Vector2KeyFrameAnimation?[bars]; _offsets = new Vector2KeyFrameAnimation?[bars];
        Height = 38; IsHitTestVisible = false; Content = _host;
        Loaded += (_, _) => { EnsureVisual(); Render(false); };
        SizeChanged += (_, _) => { if (IsLoaded) Render(false); };
        Unloaded += (_, _) => { foreach (var bar in _bars) { bar?.StopAnimation("Size"); bar?.StopAnimation("Offset"); } };
        AutomationProperties.SetName(this, L.T("Live output audio visualizer"));
    }
    public void SetColor(Brush color)
    {
        if (color is not SolidColorBrush solid || _color.Equals(solid.Color)) return;
        _color = solid.Color; ApplyColor();
    }
    private void ApplyColor()
    {
        for (var i = 0; i < _brushes.Length; i++)
            if (_brushes[i] is { } brush) brush.Color = Color.FromArgb((byte)(_color.A * (.35 + .65 * (i + 1) / _brushes.Length)), _color.R, _color.G, _color.B);
        AutomationProperties.SetHelpText(this, L.T("Visualizer color") + $": #{_color.R:X2}{_color.G:X2}{_color.B:X2}");
    }
    public void Update(double level, bool available, Brush color, bool animate)
    {
        Array.Copy(_history, 1, _history, 0, _history.Length - 1);
        _history[^1] = available ? Math.Clamp(level, 0, 1) : 0;
        SetColor(color);
        if (IsLoaded) Render(animate);
        if (_available != available)
        { _available = available; AutomationProperties.SetName(this, available ? L.T("Live output audio visualizer") : L.T("Output audio visualizer unavailable")); }
    }
    private void EnsureVisual()
    {
        if (_visual is not null) return;
        var compositor = ElementCompositionPreview.GetElementVisual(_host).Compositor;
        _visual = compositor.CreateShapeVisual(); _visual.BorderMode = CompositionBorderMode.Soft;
        for (var i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i] = compositor.CreateRoundedRectangleGeometry();
            var shape = compositor.CreateSpriteShape(bar); shape.FillBrush = _brushes[i] = compositor.CreateColorBrush();
            _visual.Shapes.Add(shape);
        }
        ElementCompositionPreview.SetElementChildVisual(_host, _visual); ApplyColor();
    }
    private void Render(bool animate)
    {
        if (_visual is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var width = (float)ActualWidth; var height = (float)ActualHeight;
        _visual.Size = new(width, height);
        // A compact dock cannot fit the 24 bars used by a full media panel.
        var visible = Math.Clamp((int)Math.Floor((width + 2) / 5), 3, _bars.Length);
        var gap = visible <= 6 ? 2f : 3f;
        var barWidth = Math.Max(.5f, (width - gap * (visible - 1)) / visible);
        for (var i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i]!;
            if (i >= visible) { bar.StopAnimation("Size"); bar.StopAnimation("Offset"); bar.Size = Vector2.Zero; continue; }
            var barHeight = Math.Clamp((float)Math.Sqrt(animate ? _history[_history.Length - visible + i] : _history[^1]) * height, Math.Min(2, height), height);
            var size = new Vector2(barWidth, barHeight);
            var offset = new Vector2(i * (barWidth + gap), (height - barHeight) / 2);
            bar.CornerRadius = new(Math.Min(barWidth, barHeight) / 2);
            if (!animate)
            { bar.StopAnimation("Size"); bar.StopAnimation("Offset"); bar.Size = size; bar.Offset = offset; }
            else
            {
                var sizeAnimation = _sizes[i] ??= bar.Compositor.CreateVector2KeyFrameAnimation();
                var offsetAnimation = _offsets[i] ??= bar.Compositor.CreateVector2KeyFrameAnimation();
                sizeAnimation.Duration = offsetAnimation.Duration = TimeSpan.FromMilliseconds(80);
                sizeAnimation.InsertKeyFrame(1, size); offsetAnimation.InsertKeyFrame(1, offset);
                bar.StartAnimation("Size", sizeAnimation); bar.StartAnimation("Offset", offsetAnimation);
            }
        }
    }
}
