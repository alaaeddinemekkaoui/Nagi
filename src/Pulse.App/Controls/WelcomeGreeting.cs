using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Pulse.App.Controls;

/// <summary>Original hand-drawn Nagi lettering. No font or image download is required.</summary>
public sealed class WelcomeGreeting : Grid
{
    private readonly List<(Path Path, double Length, bool Japanese)> _strokes = [];
    private readonly Viewbox _english = new() { Stretch = Stretch.Uniform, MaxWidth = 430 };
    private readonly Viewbox _japanese = new() { Stretch = Stretch.Uniform, MaxWidth = 300 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly System.Diagnostics.Stopwatch _time = new();

    public WelcomeGreeting()
    {
        Height = 260; HorizontalAlignment = HorizontalAlignment.Stretch;
        FlowDirection = FlowDirection.LeftToRight;
        var english = new Canvas { Width = 360, Height = 220 };
        var japanese = new Canvas { Width = 230, Height = 170 };
        // Three distinct strokes form a capital N, with a straight diagonal rather than an M-like arch.
        Add(english, "M 72,153 C 80,120 83,88 88,58", 100, false);
        Add(english, "M 88,58 C 99,80 112,125 125,151", 105, false);
        Add(english, "M 125,151 C 132,116 136,82 145,56", 105, false);
        Add(english, "M 187,113 C 169,94 145,118 153,138 C 160,156 180,138 186,117 C 175,151 195,147 207,132", 150, false);
        Add(english, "M 247,112 C 229,95 205,121 216,139 C 231,158 244,123 246,114 C 239,162 239,191 218,193 C 197,195 211,171 256,146 L 270,132", 250, false);
        Add(english, "M 284,111 C 271,138 277,156 306,132 M 291,91 L 293,88", 90, false);
        Add(japanese, "M 30,65 L 96,65", 70, true);
        Add(japanese, "M 70,35 C 72,90 55,112 34,132", 115, true);
        Add(japanese, "M 128,55 L 186,44 M 124,83 L 194,68", 140, true);
        Add(japanese, "M 153,30 L 174,130 M 183,22 L 191,33 M 199,15 L 207,26", 140, true);
        _english.Child = english; _japanese.Child = japanese;
        Children.Add(_english); Children.Add(_japanese);
        _timer.Tick += (_, _) =>
        {
            var elapsed = _time.Elapsed.TotalSeconds;
            _english.Opacity = 1 - Math.Clamp((elapsed - 2.1) / .45, 0, 1);
            _japanese.Opacity = Math.Clamp((elapsed - 2.65) / .3, 0, 1);
            var enIndex = 0; var jaIndex = 0;
            foreach (var (path, length, isJapanese) in _strokes)
            {
                var start = isJapanese ? 2.65 + jaIndex++ * .23 : enIndex++ * .22;
                var progress = Math.Clamp((elapsed - start) / .5, 0, 1);
                path.StrokeDashOffset = length / path.StrokeThickness * (1 - progress);
            }
            if (elapsed > 4.3) Stop();
        };
        Unloaded += (_, _) => Stop();
    }

    private void Add(Canvas canvas, string geometry, double length, bool japanese)
    {
        var path = (Path)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='" + geometry + "'/>");
        path.Stroke = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 218, 73, 82));
        path.StrokeThickness = 3.5; path.StrokeStartLineCap = path.StrokeEndLineCap = PenLineCap.Round;
        path.StrokeLineJoin = PenLineJoin.Round;
        path.StrokeDashArray = [length / 3.5, length / 3.5];
        canvas.Children.Add(path); _strokes.Add((path, length, japanese));
    }

    public void Start(bool reducedMotion)
    {
        Stop();
        var animate = !reducedMotion && new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        foreach (var (path, length, _) in _strokes) path.StrokeDashOffset = animate ? length / path.StrokeThickness : 0;
        _english.Opacity = 1; _japanese.Opacity = animate ? 0 : 1;
        _english.MaxHeight = animate ? 260 : 150; _japanese.MaxHeight = animate ? 260 : 110;
        _english.RenderTransform = new TranslateTransform { Y = animate ? 0 : -48 };
        _japanese.RenderTransform = new TranslateTransform { Y = animate ? 0 : 65 };
        if (!animate) return;
        _time.Restart(); _timer.Start();
    }
    public void Stop() { _timer.Stop(); _time.Stop(); }
}
