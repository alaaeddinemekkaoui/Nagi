using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Composition;
using Pulse.Core.Motion;
using System.Numerics;
using SystemComposition = Windows.UI.Composition;

namespace Pulse.App.Themes;

// One cached material surface and one vector mask. Effect inputs must be surface,
// color or backdrop brushes; Windows rejects nested effect/mask brush inputs.
internal sealed class NotchBackdropMask : IDisposable
{
    private ICompositionSupportsSystemBackdrop? _target;
    private SystemComposition.CompositionBrush? _source;
    private SystemComposition.CompositionEffectFactory? _factory;
    private SystemComposition.CompositionEffectBrush? _output;
    private SystemComposition.CompositionBackdropBrush? _desktop;
    private SystemComposition.CompositionColorBrush? _fill;
    private SystemComposition.CompositionVisualSurface? _maskSurface, _materialSurface;
    private SystemComposition.CompositionSurfaceBrush? _maskBrush, _materialBrush;
    private SystemComposition.ShapeVisual? _maskVisual;
    private SystemComposition.CompositionPathGeometry? _path;
    private SystemComposition.ContainerVisual? _materialTree;
    private SystemComposition.SpriteVisual? _materialVisual, _scrimVisual;
    private (float Width, float Height, double Drop, string Edge, double Notch, float Scale)? _last;
    public bool Active => _output is not null && _target?.SystemBackdrop == _output;

    public void Apply(ICompositionSupportsSystemBackdrop target, float width, float height, double drop, string edge, double notch,
        float scale, global::Windows.UI.Color surfaceColor, bool solid)
    {
        if (drop <= .001 || width <= 0 || height <= 0) { Restore(); return; }
        var brush = target.SystemBackdrop;
        if (brush is null) return;
        _target = target;
        if (brush != _output) _source = brush;
        if (_output is null)
        {
            var compositor = brush.Compositor;
            _path = compositor.CreatePathGeometry();
            var shape = compositor.CreateSpriteShape(_path);
            shape.FillBrush = compositor.CreateColorBrush(Microsoft.UI.Colors.White);
            _maskVisual = compositor.CreateShapeVisual(); _maskVisual.BorderMode = SystemComposition.CompositionBorderMode.Soft;
            _maskVisual.Shapes.Add(shape);
            _maskSurface = compositor.CreateVisualSurface(); _maskSurface.SourceVisual = _maskVisual;
            _maskBrush = compositor.CreateSurfaceBrush(_maskSurface);

            _fill = compositor.CreateColorBrush(surfaceColor);
            _materialVisual = compositor.CreateSpriteVisual(); _scrimVisual = compositor.CreateSpriteVisual(); _scrimVisual.Brush = _fill;
            _materialTree = compositor.CreateContainerVisual();
            _materialTree.Children.InsertAtBottom(_materialVisual); _materialTree.Children.InsertAtTop(_scrimVisual);
            _materialSurface = compositor.CreateVisualSurface(); _materialSurface.SourceVisual = _materialTree;
            _materialBrush = compositor.CreateSurfaceBrush(_materialSurface);
            _desktop = compositor.CreateHostBackdropBrush();
            using var maskedMaterial = new CompositeEffect
            {
                Mode = CanvasComposite.DestinationIn,
                Sources = { new SystemComposition.CompositionEffectSourceParameter("Material"), new SystemComposition.CompositionEffectSourceParameter("Mask") }
            };
            using var output = new CompositeEffect
            {
                Mode = CanvasComposite.SourceOver,
                Sources = { new SystemComposition.CompositionEffectSourceParameter("Desktop"), maskedMaterial }
            };
            _factory = compositor.CreateEffectFactory(output); _output = _factory.CreateBrush();
            _output.SetSourceParameter("Desktop", _desktop);
            _output.SetSourceParameter("Material", _materialBrush); _output.SetSourceParameter("Mask", _maskBrush);
        }
        var dimensions = (width, height, drop, edge, notch, scale);
        if (_last != dimensions)
        {
            var points = RestingContour.BezierPoints(width, height, edge, drop, notch, scale);
            var horizontal = edge is "Top edge" or "Bottom edge";
            var start = edge switch { "Right edge" => new Vector2(width, 0), "Bottom edge" => new Vector2(0, height), _ => Vector2.Zero };
            using var builder = new CanvasPathBuilder(CanvasDevice.GetSharedDevice());
            builder.BeginFigure(start);
            builder.AddLine(horizontal ? new(0, points[0].Y) : new(points[0].X, 0)); builder.AddLine(points[0]);
            for (var i = 0; i + 3 < points.Length; i += 3) builder.AddCubicBezier(points[i + 1], points[i + 2], points[i + 3]);
            builder.AddLine(horizontal ? new(width, points[^1].Y) : new(points[^1].X, height));
            builder.AddLine(horizontal ? new(width, start.Y) : new(start.X, height));
            builder.EndFigure(CanvasFigureLoop.Closed);
            using var geometry = CanvasGeometry.CreatePath(builder);
            _path!.Path = new SystemComposition.CompositionPath(geometry);
            var size = new Vector2(width, height);
            _maskVisual!.Size = _maskSurface!.SourceSize = size;
            _materialTree!.Size = _materialVisual!.Size = _scrimVisual!.Size = _materialSurface!.SourceSize = size;
            _last = dimensions;
        }
        if (!_fill!.Color.Equals(surfaceColor)) _fill.Color = surfaceColor;
        var material = solid ? null : _source;
        if (_materialVisual!.Brush != material) _materialVisual.Brush = material;
        if (target.SystemBackdrop != _output) target.SystemBackdrop = _output;
    }
    public void Restore()
    {
        if (Active) _target!.SystemBackdrop = _source;
    }
    public void Dispose()
    {
        Restore();
        _output?.Dispose(); _factory?.Dispose(); _desktop?.Dispose(); _fill?.Dispose();
        _maskBrush?.Dispose(); _materialBrush?.Dispose(); _maskSurface?.Dispose(); _materialSurface?.Dispose();
        _maskVisual?.Dispose(); _materialVisual?.Dispose(); _scrimVisual?.Dispose(); _materialTree?.Dispose(); _path?.Dispose();
        _output = null; _factory = null; _desktop = null; _fill = null;
        _maskBrush = null; _materialBrush = null; _maskSurface = null; _materialSurface = null;
        _maskVisual = null; _materialVisual = null; _scrimVisual = null; _materialTree = null; _path = null;
        _source = null; _target = null; _last = null;
    }
}
