using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Pulse.App.Controls;

// Paint through the rounded border instead of placing a rectangular Image over it.
// ImageBrush retains the platform's antialiased corners at every display scale.
public sealed class ArtworkImage : UserControl
{
    private readonly Border _surface = new();
    private readonly FontIcon _fallback = new() { Glyph = "\uE8D6", FontSize = 14 };
    public ArtworkImage()
    {
        var content = new Grid(); content.Children.Add(_surface); content.Children.Add(_fallback);
        Content = content; IsHitTestVisible = false;
        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => _fallback.FontSize = FontSize);
        RegisterPropertyChangedCallback(CornerRadiusProperty, (_, _) => _surface.CornerRadius = CornerRadius);
    }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(ArtworkImage), new PropertyMetadata(null, SourceChanged));
    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private static void SourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var artwork = (ArtworkImage)sender;
        artwork._fallback.Visibility = args.NewValue is ImageSource ? Visibility.Collapsed : Visibility.Visible;
        artwork._surface.Background = args.NewValue is ImageSource image
            ? new ImageBrush { ImageSource = image, Stretch = Stretch.UniformToFill }
            : null;
    }
}
