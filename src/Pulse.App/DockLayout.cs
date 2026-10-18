using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private bool? _horizontalRail;
    private double _railExtent;
    private double RequestedRailExtent => DockPresentation.RailExtent(ViewModel.Settings.Workspace(ViewModel.Profile),
        ViewModel.Settings.Position.Orientation == "Horizontal", ViewModel.Settings.ShowMedia, ViewModel.Settings.WeatherEnabled, ViewModel.Metrics.Power);
    private void ConfigureRail(bool horizontal)
    {
        if (_horizontalRail == horizontal) return;
        _horizontalRail = horizontal;
        Compact.RowDefinitions.Clear(); Compact.ColumnDefinitions.Clear();
        var buttons = new FrameworkElement[] { CompactBrand, RailSearchButton, ProfileDockPanel, CompactCpuButton, RailGpuButton,
            CompactMediaButton, RailObsButton, RailMicrophoneButton, RailWeatherButton, CompactBatteryButton };
        var widths = new[] { 32d, 32, double.NaN, 56, 56, 168, 32, 30, 44, 44 };
        var heights = new[] { 32d, 32, double.NaN, 28, 28, 50, 32, 30, 30, 30 };
        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetRow(buttons[i], horizontal ? 0 : i); Grid.SetColumn(buttons[i], horizontal ? i : 0);
            buttons[i].Width = horizontal ? widths[i] : double.NaN;
            buttons[i].Height = horizontal ? 48 : heights[i];
            if (buttons[i] is Button sizedButton) sizedButton.Padding = new(4);
            if (buttons[i] is Button button && !_themeAccessibility.HighContrast)
            {
                button.Resources["ButtonBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 255, 255, 255));
                button.Resources["ButtonBackgroundPressed"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(32, 255, 255, 255));
            }
            buttons[i].MinHeight = 0; buttons[i].MinWidth = 0;
            buttons[i].Margin = horizontal ? new(2, 0, 2, 0) : new(0, 2, 0, 2);
            if (horizontal) Compact.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            else Compact.RowDefinitions.Add(new() { Height = GridLength.Auto });
        }
        Compact.RowSpacing = Compact.ColumnSpacing = 0;
        CompactCpuButton.Padding = RailGpuButton.Padding = new(3, 0, 3, 0);
        if (!horizontal) RailObsButton.Padding = RailWeatherButton.Padding = CompactBatteryButton.Padding = new(0);
        RailWeatherValue.FontSize = horizontal ? 11 : 10;
        AppFolderButton.Height = horizontal ? 36 : 32; AppFolderButton.MinHeight = 0;
        ProfileDockPanel.Height = double.NaN; ProfileDockPanel.VerticalAlignment = VerticalAlignment.Center;
        ProfileDockPanel.Spacing = 4;
        Compact.Padding = horizontal ? new(8, 4, 8, 4) : new(5, 8, 5, 8);
        CompactMediaContents.RowDefinitions.Clear(); CompactMediaContents.ColumnDefinitions.Clear();
        var parts = new FrameworkElement[] { CompactArtworkHost, CompactMediaStatus, CompactMusicVisualizer, CompactAudioValue };
        for (var i = 0; i < parts.Length; i++)
        {
            Grid.SetRow(parts[i], horizontal ? 0 : i); Grid.SetColumn(parts[i], horizontal ? i : 0);
            if (horizontal) CompactMediaContents.ColumnDefinitions.Add(new() { Width = i == 1 ? new(1, GridUnitType.Star) : GridLength.Auto });
            else CompactMediaContents.RowDefinitions.Add(new() { Height = GridLength.Auto });
        }
        CompactMediaContents.ColumnSpacing = horizontal ? 6 : 0; CompactMediaContents.RowSpacing = horizontal ? 0 : 2;
        CompactMediaContents.HorizontalAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CompactMediaButton.HorizontalContentAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CompactMediaStatus.HorizontalAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        CompactMediaStatus.VerticalAlignment = VerticalAlignment.Center;
        CompactTrackTitle.HorizontalAlignment = CompactSecondaryValue.HorizontalAlignment = HorizontalAlignment.Left;
        CompactTrackTitle.MaxWidth = 90;
        CompactArtworkHost.Width = CompactArtworkHost.Height = 26;
        CompactMusicVisualizer.Visibility = CompactSecondaryLabel.Visibility = Visibility.Collapsed;
        CompactAudioValue.MinWidth = horizontal ? 34 : 0;
        CompactAudioValue.TextAlignment = TextAlignment.Center;
        RailViewport.HorizontalScrollBarVisibility = RailViewport.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        RailViewport.HorizontalScrollMode = RailViewport.VerticalScrollMode = ScrollMode.Disabled;
        RailViewport.ZoomMode = ZoomMode.Disabled;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(RailViewport, horizontal ? L.T("Nagi horizontal rail") : L.T("Nagi vertical rail"));
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
        _railExtent = RequestedRailExtent;
        RailViewport.Visibility = resting ? Visibility.Collapsed : Visibility.Visible;
        EdgeDrop.Width = horizontal ? 132 : 18; EdgeDrop.Height = horizontal ? 18 : 132;
        Compact.MinWidth = Compact.MinHeight = 0;
        if (horizontal)
        {
            var top = position.Anchor == "Top edge";
            var terse = !resting && width < _railExtent - 1;
            CompactMediaButton.Width = terse ? 72 : 168;
            CompactMediaButton.Padding = terse ? new(2, 0, 2, 0) : new(4);
            CompactMediaContents.ColumnSpacing = terse ? 2 : 6;
            CompactMediaStatus.Visibility = terse ? Visibility.Collapsed : Visibility.Visible;
            CompactTrackTitle.Visibility = Visibility.Visible;
            Compact.Width = Math.Max(1, _railExtent - (terse ? 96 : 0)); Compact.Height = DockPresentation.HorizontalHeight;
            RailViewport.Width = width; RailViewport.Height = DockPresentation.HorizontalHeight;
            RailViewport.HorizontalAlignment = HorizontalAlignment.Center;
            RailViewport.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            PeekHost.Width = Expanded.Width = width;
            PeekHost.Height = Expanded.Height = Math.Max(0, height - DockPresentation.HorizontalHeight);
            PeekHost.HorizontalAlignment = Expanded.HorizontalAlignment = HorizontalAlignment.Center;
            PeekHost.VerticalAlignment = Expanded.VerticalAlignment = top ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            EdgeDrop.HorizontalAlignment = HorizontalAlignment.Center;
            EdgeDrop.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        }
        else
        {
            var left = position.Anchor == "Left edge";
            Compact.Width = DockPresentation.VerticalWidth; Compact.Height = _railExtent;
            CompactMediaButton.Padding = new(4, 2, 4, 2);
            CompactMediaStatus.Visibility = CompactTrackTitle.Visibility = Visibility.Collapsed;
            RailViewport.Width = DockPresentation.VerticalWidth; RailViewport.Height = Math.Min(_railExtent, height);
            RailViewport.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            RailViewport.VerticalAlignment = VerticalAlignment.Center;
            PeekHost.Width = Expanded.Width = Math.Max(0, width - DockPresentation.VerticalWidth);
            PeekHost.Height = Expanded.Height = height;
            PeekHost.HorizontalAlignment = Expanded.HorizontalAlignment = left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            PeekHost.VerticalAlignment = Expanded.VerticalAlignment = VerticalAlignment.Center;
            EdgeDrop.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            EdgeDrop.VerticalAlignment = VerticalAlignment.Center;
        }
        EdgeDrop.IsHitTestVisible = resting; Compact.IsHitTestVisible = !resting;
        ApplyWorkspaceVisibility();
        DockPinIndicator.Visibility = _dock.Pinned ? Visibility.Visible : Visibility.Collapsed;
    }
}
