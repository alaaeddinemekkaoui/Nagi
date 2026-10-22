using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private void ApplyThemeBackground()
    {
        var visible = _expanded && ViewModel.Settings.Theme == "Akeno" && !_themeAccessibility.HighContrast;
        AkenoDecor.Visibility = Visibility.Collapsed;
        AkenoBackgroundImage.Opacity = ViewModel.Settings.GlassEnabled ? .18 : .58;
        AkenoGradient.Opacity = ViewModel.Settings.GlassEnabled ? .35 : 1;
        AkenoBackgroundImage.Visibility = visible && ViewModel.Settings.AkenoBackground == "Samurai" ? Visibility.Visible : Visibility.Collapsed;
        AkenoGradient.Visibility = visible && ViewModel.Settings.AkenoBackground == "Gradient" ? Visibility.Visible : Visibility.Collapsed;
    }
    private void UpdateSurfaceMaterial()
    {
        if (_themeAccessibility.HighContrast)
        { WindowSurface.SetColor(((SolidColorBrush)ThemeBrush("CanvasBrush")).Color); return; }
        var color = NotchColor;
        color.A = Themes.AppAppearance.UsesGlass(ViewModel.Settings) && SystemBackdrop is not null
            ? Themes.HaloAcrylicBackdrop.SurfaceAlpha(Shell.ActualTheme == ElementTheme.Light) : (byte)255;
        _glass.SetSurfaceColor(color);
        // One antialiased material edge. Painting the scrim again in XAML doubles
        // fractional coverage and leaves a darker outline around the silhouette.
        WindowSurface.SetColor(_glass.ContourMasked ? Microsoft.UI.Colors.Transparent : color);
    }
}
