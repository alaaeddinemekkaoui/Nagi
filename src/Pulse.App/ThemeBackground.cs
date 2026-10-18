using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private void ApplyThemeBackground()
    {
        var visible = _expanded && ViewModel.Settings.Theme == "Akeno" && !_themeAccessibility.HighContrast;
        AkenoDecor.Visibility = Visibility.Collapsed;
        AkenoBackgroundImage.Visibility = visible && ViewModel.Settings.AkenoBackground == "Samurai" ? Visibility.Visible : Visibility.Collapsed;
        AkenoGradient.Visibility = visible && ViewModel.Settings.AkenoBackground == "Gradient" ? Visibility.Visible : Visibility.Collapsed;
    }
    private void UpdateSurfaceMaterial()
    {
        if (_themeAccessibility.HighContrast)
        { WindowSurface.SetColor(((SolidColorBrush)ThemeBrush("CanvasBrush")).Color); return; }
        var color = ((SolidColorBrush)ThemeBrush("SurfaceBrush")).Color;
        if (_materialResting) { WindowSurface.SetColor(NotchColor); return; }
        if (SystemBackdrop is not null && !ViewModel.Settings.LuaUiEnabled)
            color.A = ViewModel.Settings.Theme == "Akeno" ? (byte)74 : Shell.ActualTheme == ElementTheme.Light ? (byte)160 : (byte)24;
        else color.A = 255;
        WindowSurface.SetColor(color);
    }
}
