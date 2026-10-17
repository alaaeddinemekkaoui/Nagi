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
    public void SetAkeno(bool enabled) { _akeno = enabled; ApplyPalette(); }
    public static bool IsSupported => DesktopAcrylicController.IsSupported();
    public string MaterialStatus => _controller?.State.ToString() ?? "Off";
    public void SetLight(bool light) { _light = light; ApplyPalette(); }
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (!IsSupported) return;
        _controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
        _configuration = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
        _configuration.IsInputActive = true;
        _controller.SetSystemBackdropConfiguration(_configuration);
        _controller.AddSystemBackdropTarget(connectedTarget);
        ApplyPalette();
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        // The framework maintains this same configuration object. The base override
        // throws E_INVALIDARG on theme changes in this custom controller host.
        // Do not reconnect the target or forward the notification back to the base.
        // It can also arrive during base.OnTargetConnected, before our setup finishes.
        if (_controller is null || _configuration is null) return;
        _configuration.IsInputActive = true;
        ApplyPalette();
    }
    private void ApplyPalette()
    {
        if (_controller is null) return;
        if (_akeno)
        {
            _controller.TintColor = Color.FromArgb(255, 9, 7, 8);
            _controller.FallbackColor = Color.FromArgb(255, 16, 11, 13);
            _controller.TintOpacity = .62f; _controller.LuminosityOpacity = .66f;
            return;
        }
        _controller.TintColor = _light ? Color.FromArgb(255, 246, 248, 253) : Color.FromArgb(255, 24, 27, 38);
        _controller.FallbackColor = _light ? Color.FromArgb(255, 241, 244, 249) : Color.FromArgb(255, 27, 30, 40);
        _controller.TintOpacity = _light ? .35f : .30f;
        _controller.LuminosityOpacity = _light ? .72f : .58f;
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        _controller?.RemoveSystemBackdropTarget(target);
        _controller?.Dispose(); _controller = null;
        _configuration = null;
        base.OnTargetDisconnected(target);
    }
}
