using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Pulse.Windows;

namespace Pulse.App.Controls;

// Familiar Windows outline symbols where available; otherwise use the installed app's own icon.
public sealed class AppIcon : UserControl
{
    private readonly string? _nativeTarget;
    private XamlRoot? _observedRoot;
    private int _requestedPixels;
    private int _loadVersion;
    public AppIcon(string title, string? target, Brush foreground, double size = 20)
    {
        Width = Height = size; IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(this, L.T("Icon for ") + title);
        var key = (target ?? title).ToLowerInvariant();
        var glyph = key.Contains("windowsterminal") || key == "terminal" ? "\uE756"
            : key.Contains("windowscalculator") || key == "calc" ? "\uE8EF"
            : key.Contains("windowsnotepad") ? "\uE70B"
            : key is "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel" ? "\uE713" : null;
        Content = new FontIcon { Glyph = glyph ?? "\uECAA", FontSize = size, Foreground = foreground };
        if (glyph is not null) AutomationProperties.SetHelpText(this, L.T("Outline icon"));
        else if (!string.IsNullOrWhiteSpace(target))
        {
            _nativeTarget = target;
            Loaded += (_, _) =>
            {
                _observedRoot = XamlRoot;
                if (_observedRoot is not null) _observedRoot.Changed += RootChanged;
                RefreshNativeIcon();
            };
            Unloaded += (_, _) =>
            {
                if (_observedRoot is not null) _observedRoot.Changed -= RootChanged;
                _observedRoot = null; _requestedPixels = 0; _loadVersion++;
            };
        }
    }
    private void RootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => RefreshNativeIcon();
    private void RefreshNativeIcon()
    {
        if (_nativeTarget is null) return;
        var physicalSize = Width * (XamlRoot?.RasterizationScale ?? 1) * 2;
        // Keep a larger shell source for the small dock and its hover cards.
        var pixels = physicalSize <= 128 ? 128 : 256;
        if (_requestedPixels == pixels) return;
        _requestedPixels = pixels;
        _ = LoadNativeAsync(_nativeTarget, pixels, ++_loadVersion);
    }
    private async Task LoadNativeAsync(string target, int pixelSize, int version)
    {
        try
        {
            var pixels = await ApplicationIcons.ReadAsync(target, pixelSize);
            if (pixels is null || version != _loadVersion) return;
            var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
            using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(pixels.Bgra);
            bitmap.Invalidate();
            Content = new Image { Source = bitmap, Stretch = Stretch.Uniform, Width = Width, Height = Height };
            AutomationProperties.SetHelpText(this, L.T("Native app icon"));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or ObjectDisposedException)
        { /* The outline fallback stays visible if the app disappears during catalog refresh. */ }
    }
}
