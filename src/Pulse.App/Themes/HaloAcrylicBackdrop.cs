using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Pulse.App.Themes;

/// <summary>Native desktop acrylic, kept active for the non-activating compact utility window.</summary>
public sealed class HaloAcrylicBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;
    private bool _light;
    private bool _akeno;
    private Color? _tint;
    private ICompositionSupportsSystemBackdrop? _target;
    private readonly NotchBackdropMask _contour = new();
    private (float Width, float Height, double Drop, string Edge, double Notch, float Scale) _shape;
    private Color _surfaceColor = Color.FromArgb(24, 16, 16, 18);
    private Color? _solidSurface;
    public bool ContourMasked => _contour.Active;
    public event Action? ContourMaskChanged;
    public void SetContour(float width, float height, double drop, string edge, double notch = 0, float scale = 1)
    {
        _shape = (width, height, drop, edge, notch, scale); ApplyContour();
    }
    public void SetSurfaceColor(Color color)
    { if (_surfaceColor.Equals(color)) return; _surfaceColor = color; ApplyContour(); }
    public void SetSolidSurface(Color? color)
    { if (Nullable.Equals(_solidSurface, color)) return; _solidSurface = color; ApplyContour(); }
    private void ApplyContour()
    {
        var wasActive = _contour.Active;
        if (_target is not null) _contour.Apply(_target, _shape.Width, _shape.Height, _shape.Drop, _shape.Edge, _shape.Notch,
            _shape.Scale, _solidSurface ?? _surfaceColor, _solidSurface.HasValue);
        if (wasActive != _contour.Active) ContourMaskChanged?.Invoke();
    }
    public void SetAkeno(bool enabled) { if (_akeno == enabled) return; _akeno = enabled; ApplyPalette(); }
    public void SetTint(Color? color) { if (Nullable.Equals(_tint, color)) return; _tint = color; ApplyPalette(); }
    public static bool IsSupported => DesktopAcrylicController.IsSupported();
    // One light scrim over native acrylic; never fade the window's text or icons.
    public static byte SurfaceAlpha(bool light) => light ? (byte)48 : (byte)24;
    public static Color SurfaceColor(bool light, bool akeno, bool glass)
    {
        var color = light ? Color.FromArgb(255, 241, 244, 249)
            : akeno ? Color.FromArgb(255, 16, 11, 13) : Color.FromArgb(255, 16, 16, 18);
        color.A = glass ? SurfaceAlpha(light) : (byte)255;
        return color;
    }
    public string MaterialStatus => _controller?.State.ToString() ?? "Off";
    public void SetLight(bool light) { if (_light == light) return; _light = light; ApplyPalette(); }
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (!IsSupported) return;
        _controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
        _configuration = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
        _light = _configuration.Theme == SystemBackdropTheme.Light;
        _configuration.IsInputActive = true;
        _controller.SetSystemBackdropConfiguration(_configuration);
        _controller.AddSystemBackdropTarget(connectedTarget);
        _target = connectedTarget;
        ApplyPalette();
        ApplyContour();
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        // The framework maintains this same configuration object. The base override
        // throws E_INVALIDARG on theme changes in this custom controller host.
        // Do not reconnect the target or forward the notification back to the base.
        // It can also arrive during base.OnTargetConnected, before our setup finishes.
        if (_controller is null || _configuration is null) return;
        _light = _configuration.Theme == SystemBackdropTheme.Light;
        _configuration.IsInputActive = true;
        ApplyPalette();
        ApplyContour();
    }
    private void ApplyPalette()
    {
        if (_controller is null) return;
        if (_tint is { } tint)
        {
            _controller.TintColor = tint;
            var neutral = _light ? Color.FromArgb(255, 241, 244, 249) : Color.FromArgb(255, 27, 30, 40);
            _controller.FallbackColor = Color.FromArgb(255, (byte)(neutral.R * .84 + tint.R * .16),
                (byte)(neutral.G * .84 + tint.G * .16), (byte)(neutral.B * .84 + tint.B * .16));
            _controller.TintOpacity = _light ? .20f : .18f;
            _controller.LuminosityOpacity = _light ? .80f : .30f;
            return;
        }
        if (_akeno)
        {
            _controller.TintColor = Color.FromArgb(255, 9, 7, 8);
            _controller.FallbackColor = Color.FromArgb(255, 16, 11, 13);
            _controller.TintOpacity = .18f; _controller.LuminosityOpacity = .30f;
            return;
        }
        _controller.TintColor = _light ? Color.FromArgb(255, 246, 248, 253) : Color.FromArgb(255, 16, 16, 18);
        _controller.FallbackColor = _light ? Color.FromArgb(255, 241, 244, 249) : Color.FromArgb(255, 16, 16, 18);
        _controller.TintOpacity = _light ? .20f : .18f;
        _controller.LuminosityOpacity = _light ? .80f : .30f;
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        var wasMasked = _contour.Active; _contour.Dispose(); _target = null;
        if (wasMasked) ContourMaskChanged?.Invoke();
        _controller?.RemoveSystemBackdropTarget(target);
        _controller?.Dispose(); _controller = null;
        _configuration = null;
        base.OnTargetDisconnected(target);
    }
}
