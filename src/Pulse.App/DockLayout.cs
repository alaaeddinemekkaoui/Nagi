using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private bool? _horizontalRail;
    private void ConfigureRail(bool horizontal)
    {
        if (_horizontalRail == horizontal) return;
        _horizontalRail = horizontal;
        Compact.RowDefinitions.Clear(); Compact.ColumnDefinitions.Clear();
        var buttons = new FrameworkElement[] { CompactBrand, RailSearchButton, ProfileDockPanel, CompactCpuButton, RailGpuButton,
            CompactMediaButton, RailObsButton, RailMicrophoneButton, RailWeatherButton, CompactBatteryButton };
        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetRow(buttons[i], horizontal ? 0 : i); Grid.SetColumn(buttons[i], horizontal ? i : 0);
            buttons[i].Width = horizontal ? new[] { 40d, 40, double.NaN, 54, 54, double.NaN, 40, 40, 54, 48 }[i] : double.NaN;
            if (horizontal) Compact.ColumnDefinitions.Add(new() { Width = i == 5 ? new(1, GridUnitType.Star) : GridLength.Auto });
            else Compact.RowDefinitions.Add(new() { Height = i == 5 ? new(1, GridUnitType.Star) : GridLength.Auto });
        }
        Compact.RowSpacing = horizontal ? 0 : 5; Compact.ColumnSpacing = horizontal ? 7 : 0;
        Compact.Padding = horizontal ? new(10, 5, 10, 5) : new(7, 10, 7, 10);
        CompactMediaContents.RowDefinitions.Clear(); CompactMediaContents.ColumnDefinitions.Clear();
        var mediaParts = new FrameworkElement[] { CompactArtworkHost, CompactMediaStatus, CompactMusicVisualizer, CompactAudioValue };
        for (var i = 0; i < mediaParts.Length; i++)
        {
            Grid.SetRow(mediaParts[i], horizontal ? 0 : i); Grid.SetColumn(mediaParts[i], horizontal ? i : 0);
            if (horizontal) CompactMediaContents.ColumnDefinitions.Add(new() { Width = i == 1 ? new(1, GridUnitType.Star) : GridLength.Auto });
            else CompactMediaContents.RowDefinitions.Add(new() { Height = GridLength.Auto });
        }
        CompactMediaContents.ColumnSpacing = horizontal ? 8 : 0;
        CompactMediaContents.RowSpacing = horizontal ? 0 : 3;
        CompactMediaContents.HorizontalAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CompactMediaButton.HorizontalContentAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CompactMediaStatus.HorizontalAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CompactSecondaryLabel.HorizontalAlignment = CompactSecondaryValue.HorizontalAlignment = horizontal ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        CompactTrackTitle.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        CompactTrackTitle.MaxWidth = horizontal ? 220 : 54;
        CompactMusicVisualizer.Width = horizontal ? 48 : 36;
        RailWeatherButton.Width = horizontal ? 54 : double.NaN;
        RailViewport.HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        RailViewport.VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
        RailViewport.HorizontalScrollMode = horizontal ? ScrollMode.Enabled : ScrollMode.Disabled;
        RailViewport.VerticalScrollMode = horizontal ? ScrollMode.Disabled : ScrollMode.Enabled;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(RailViewport, horizontal ? "Nagi horizontal rail" : "Nagi vertical rail");
    }
    private void PlaceDockContent(bool resting, double width, double height)
    {
        RailViewport.Visibility = _controlCenter ? Visibility.Collapsed : Visibility.Visible;
        if (_controlCenter)
        {
            Expanded.Width = width; Expanded.Height = height;
            Expanded.HorizontalAlignment = HorizontalAlignment.Center; Expanded.VerticalAlignment = VerticalAlignment.Center;
            return;
        }
        var position = ViewModel.Settings.Position;
        var horizontal = position.Orientation == "Horizontal";
        ConfigureRail(horizontal);
        RailViewport.Visibility = resting ? Visibility.Collapsed : Visibility.Visible;
        EdgeDrop.Width = horizontal ? 132 : 18; EdgeDrop.Height = horizontal ? 18 : 132;
        if (horizontal)
        {
            var top = position.Anchor == "Top edge";
            Compact.Width = double.NaN; Compact.MinWidth = 740; Compact.Height = 76; Compact.MinHeight = 0;
            RailViewport.Width = width; RailViewport.Height = 76;
            RailViewport.HorizontalAlignment = Compact.HorizontalAlignment = HorizontalAlignment.Center;
            RailViewport.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            Compact.VerticalAlignment = VerticalAlignment.Center;
            PeekHost.Width = width; PeekHost.Height = Math.Max(0, height - 76);
            Expanded.Width = width; Expanded.Height = Math.Max(0, height - 76);
            PeekHost.HorizontalAlignment = Expanded.HorizontalAlignment = HorizontalAlignment.Center;
            PeekHost.VerticalAlignment = Expanded.VerticalAlignment = top ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            EdgeDrop.HorizontalAlignment = HorizontalAlignment.Center;
            EdgeDrop.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        }
        else
        {
            var left = position.Anchor == "Left edge";
            Compact.Width = 80; Compact.MinWidth = 0; Compact.Height = double.NaN; Compact.MinHeight = 560;
            RailViewport.Width = 80; RailViewport.Height = Math.Min(560, height);
            RailViewport.HorizontalAlignment = Compact.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            RailViewport.VerticalAlignment = Compact.VerticalAlignment = VerticalAlignment.Center;
            PeekHost.Width = Expanded.Width = Math.Max(0, width - 80);
            PeekHost.Height = Expanded.Height = height;
            PeekHost.HorizontalAlignment = Expanded.HorizontalAlignment = left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            PeekHost.VerticalAlignment = Expanded.VerticalAlignment = VerticalAlignment.Center;
            EdgeDrop.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            EdgeDrop.VerticalAlignment = VerticalAlignment.Center;
        }
        EdgeDrop.IsHitTestVisible = resting; Compact.IsHitTestVisible = !resting;
        CompactSecondaryValue.Visibility = Visibility.Visible;
        ApplyWorkspaceVisibility();
        DockPinIndicator.Visibility = _dock.Pinned ? Visibility.Visible : Visibility.Collapsed;
    }
}
