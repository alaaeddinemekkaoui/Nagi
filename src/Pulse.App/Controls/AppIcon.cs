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
    public AppIcon(string title, string? target, Brush foreground, double size = 20)
    {
        Width = Height = size; IsHitTestVisible = false;
        AutomationProperties.SetName(this, "Icon for " + title);
        var key = (target ?? title).ToLowerInvariant();
        var glyph = key.Contains("windowsterminal") || key == "terminal" ? "\uE756"
            : key.Contains("windowscalculator") || key == "calc" ? "\uE8EF"
            : key.Contains("windowsnotepad") ? "\uE70B"
            : key is "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel" ? "\uE713" : null;
        Content = new FontIcon { Glyph = glyph ?? "\uE71D", FontSize = size, Foreground = foreground };
        if (glyph is not null) AutomationProperties.SetHelpText(this, "Outline icon");
        else if (!string.IsNullOrWhiteSpace(target)) _ = LoadNativeAsync(target);
    }
    private async Task LoadNativeAsync(string target)
    {
        try
        {
            var pixels = await ApplicationIcons.ReadAsync(target);
            if (pixels is null) return;
            var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
            using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(pixels.Bgra);
            bitmap.Invalidate();
            Content = new Image { Source = bitmap, Stretch = Stretch.Uniform, Width = Width, Height = Height };
            AutomationProperties.SetHelpText(this, "Native app icon");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or ObjectDisposedException)
        { /* The outline fallback stays visible if the app disappears during catalog refresh. */ }
    }
}
