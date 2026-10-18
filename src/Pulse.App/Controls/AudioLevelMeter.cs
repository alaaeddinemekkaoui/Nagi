using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Pulse.App.Controls;

public sealed class AudioLevelMeter : UserControl
{
    private readonly Rectangle _fill = new() { Height = 4, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
    private double _level;
    public AudioLevelMeter()
    {
        Height = 8; IsHitTestVisible = false;
        Content = new Border { Height = 4, CornerRadius = new(2), Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(35, 128, 128, 128)), Child = _fill };
        Loaded += (_, _) => ElementCompositionPreview.GetElementVisual(_fill).Scale = new((float)_level, 1, 1);
    }
    public void Update(double level, bool available, Brush accent)
    {
        _fill.Fill = accent;
        _level = Math.Clamp(level, 0, 1);
        if (_fill.IsLoaded) ElementCompositionPreview.GetElementVisual(_fill).Scale = new Vector3((float)_level, 1, 1);
        AutomationProperties.SetName(this, available ? L.F($"System audio level {level * 100:0}%") : L.T("System audio level unavailable"));
    }
}
