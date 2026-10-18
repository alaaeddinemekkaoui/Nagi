using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Pulse.App.Controls;

public sealed class MetricCard : UserControl
{
    private readonly TextBlock _label = new() { FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Medium };
    private readonly TextBlock _value = new() { FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new(0, 4, 0, 0) };
    private readonly TextBlock _detail = new() { FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Polyline _line = new() { Stroke = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 235, 132, 158)), StrokeThickness = 1.6, Height = 17, Stretch = Stretch.Fill, Margin = new(0, 8, 0, 0) };
    private readonly Queue<double> _history = new();
    private Brush? _customAccent;
    public void Customize(double radius, Brush accent)
    {
        if (Content is Border border) border.CornerRadius = new(radius);
        _customAccent = accent; UpdateChartColor();
    }
    public void Rename(string label)
    {
        _label.Text = label;
        AutomationProperties.SetName(this, label + ": " + _value.Text + ". " + _detail.Text);
    }
    public MetricCard()
    {
        var border = new Border { Style = (Style)Application.Current.Resources["Card"] };
        _label.Opacity = .8;
        _detail.Opacity = .8;
        var stack = new StackPanel(); stack.Children.Add(_label); stack.Children.Add(_value); stack.Children.Add(_detail); stack.Children.Add(_line);
        border.Child = stack;
        Content = border;
        ActualThemeChanged += (_, _) => UpdateChartColor();
        UpdateChartColor();
        AutomationProperties.SetName(this, L.T("System metric"));
    }
    private void UpdateChartColor() => _line.Stroke = _customAccent ?? new SolidColorBrush(ActualTheme == ElementTheme.Light
        ? global::Windows.UI.Color.FromArgb(255, 172, 36, 76) : global::Windows.UI.Color.FromArgb(255, 255, 139, 163));
    public void Update(string label, string value, string detail, double? sample, bool append = true)
    {
        _label.Text = label; _value.Text = value; _detail.Text = detail;
        AutomationProperties.SetName(this, label + ": " + value + ". " + detail);
        if (sample.HasValue && append) { _history.Enqueue(sample.Value); while (_history.Count > 28) _history.Dequeue(); }
        if (_history.Count < 2) { _line.Visibility = Visibility.Collapsed; return; }
        _line.Visibility = Visibility.Visible;
        var values = _history.ToArray();
        var maximum = Math.Max(100, values.Max());
        _line.Points = new PointCollection();
        for (var i = 0; i < values.Length; i++) _line.Points.Add(new Point(i * 5, 24 - values[i] / maximum * 22));
    }
}
