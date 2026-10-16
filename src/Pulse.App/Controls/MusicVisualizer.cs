using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Numerics;

namespace Pulse.App.Controls;

// A history of measured output peaks, not a simulated frequency spectrum.
public sealed class MusicVisualizer : UserControl
{
    private readonly Rectangle[] _bars = new Rectangle[24];
    private readonly double[] _history = new double[24];
    public MusicVisualizer()
    {
        Height = 38; IsHitTestVisible = false;
        var panel = new Grid { ColumnSpacing = 3, VerticalAlignment = VerticalAlignment.Stretch };
        for (var i = 0; i < _bars.Length; i++)
        {
            panel.ColumnDefinitions.Add(new());
            var bar = _bars[i] = new() { RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Stretch };
            Grid.SetColumn(bar, i); panel.Children.Add(bar);
        }
        Content = panel;
        Loaded += (_, _) => Render(false);
        AutomationProperties.SetName(this, "Live output audio visualizer");
    }
    public void Update(double level, bool available, Brush color, bool animate)
    {
        Array.Copy(_history, 1, _history, 0, _history.Length - 1);
        _history[^1] = available ? Math.Clamp(level, 0, 1) : 0;
        foreach (var bar in _bars) bar.Fill = color;
        if (IsLoaded) Render(animate);
        AutomationProperties.SetName(this, available ? "Live output audio visualizer" : "Output audio visualizer unavailable");
    }
    private void Render(bool animate)
    {
        for (var i = 0; i < _bars.Length; i++)
        {
            var visual = ElementCompositionPreview.GetElementVisual(_bars[i]);
            visual.CenterPoint = new(0, (float)_bars[i].ActualHeight / 2, 0);
            var scale = new Vector3(1, (float)Math.Max(.035, Math.Sqrt(animate ? _history[i] : _history[^1])), 1);
            visual.Opacity = .35f + .65f * (i + 1) / _bars.Length;
            visual.StopAnimation("Scale");
            if (!animate) visual.Scale = scale;
            else
            {
                var transition = visual.Compositor.CreateVector3KeyFrameAnimation();
                transition.InsertKeyFrame(1, scale); transition.Duration = TimeSpan.FromMilliseconds(70);
                visual.StartAnimation("Scale", transition);
            }
        }
    }
}
