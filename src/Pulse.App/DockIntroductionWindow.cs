using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pulse.Windows;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace Pulse.App;

public sealed class DockIntroductionWindow : Window
{
    private readonly DockSpotlight _spotlight;
    private readonly Button _okay;
    private readonly HaloWindowManager _frame;
    public DockIntroductionWindow(HaloFrame dock, string edge, ElementTheme theme, Action dismissed)
    {
        Title = "Nagi · hello";
        var content = new StackPanel { Spacing = 16, Padding = new(28), RequestedTheme = theme };
        content.Children.Add(new Controls.PulseMark { Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(new TextBlock { Text = "Thanks for downloading Nagi!", FontSize = 23, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        var direction = edge switch { "Top edge" => "↑ At the top of your screen", "Bottom edge" => "↓ At the bottom of your screen", "Left edge" => "← On the left of your screen", _ => "→ On the right of your screen" };
        content.Children.Add(new TextBlock { Text = direction, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Your tiny desktop buddy has claimed this little spot. Hover the uncovered edge drop to wake Nagi, or choose Okay.\n\nEnjoy your new desk buddy. Please do not water it.", FontSize = 14, TextWrapping = TextWrapping.Wrap });
        _okay = new Button { Content = "Okay, tiny desk buddy!", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new(0, 4, 0, 0) };
        AutomationProperties.SetName(_okay, "Okay, got it"); _okay.Click += (_, _) => Close();
        content.Padding = new(0);
        var layout = new Grid { Padding = new(24), RowSpacing = 16 };
        layout.RowDefinitions.Add(new()); layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Grid.SetRow(_okay, 1); layout.Children.Add(_okay);
        layout.KeyDown += (_, args) => { if (args.Key == VirtualKey.Escape) { args.Handled = true; Close(); } };
        var color = theme == ElementTheme.Light ? Microsoft.UI.ColorHelper.FromArgb(255, 246, 248, 253) : Microsoft.UI.ColorHelper.FromArgb(255, 25, 27, 36);
        Content = new Border { Child = layout, Background = new SolidColorBrush(color), RequestedTheme = theme };
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        _spotlight = new(dock);
        var monitor = _spotlight.Bounds;
        var width = (int)Math.Min(440 * dock.Scale, monitor.Width - 32);
        var height = (int)Math.Min(420 * dock.Scale, monitor.Height - 32);
        _frame = new(WindowNative.GetWindowHandle(this)); _frame.PrepareControlCenter(new());
        _frame.ApplyFrame(new(monitor.X + (monitor.Width - width) / 2, monitor.Y + (monitor.Height - height) / 2, width, height, 22 * dock.Scale, dock.Scale));
        Closed += (_, _) => { _spotlight.Dispose(); _frame.Dispose(); dismissed(); };
    }
    public void ShowIntroduction() { _spotlight.Show(); Activate(); _okay.Focus(FocusState.Programmatic); }
}
