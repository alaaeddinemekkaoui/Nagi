using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private (string Title, string Artist, bool Available, bool Playing)? _mediaPreviewLayout;

    private double MeasurePreviewHeight(double width)
    {
        PeekHost.Visibility = Visibility.Visible;
        var contentWidth = Math.Max(1, width - PeekHost.Padding.Left - PeekHost.Padding.Right);
        PeekContent.Width = contentWidth;
        var available = new Size(contentWidth, double.PositiveInfinity);
        PeekContent.Measure(available);
        PreviewHeader.Measure(available);
        PreviewMoreButton.Measure(available);
        return Math.Ceiling(PeekContent.DesiredSize.Height + PreviewHeader.DesiredSize.Height + PreviewMoreButton.DesiredSize.Height
            + PeekHost.Padding.Top + PeekHost.Padding.Bottom + PeekHost.RowSpacing * 2 + 2);
    }

    private void RefreshMediaPreviewLayout()
    {
        var media = ViewModel.Media;
        var current = (media.Title, media.Artist, media.Available, media.Playing);
        if (_mediaPreviewLayout == current) return;
        _mediaPreviewLayout = current;
        if (PreviewVisible && _preview == PreviewKind.Media) Layout(true);
    }
}
