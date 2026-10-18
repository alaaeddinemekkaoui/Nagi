using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Pulse.Core.Motion;
using Pulse.Windows;

namespace Pulse.App.Motion;

/// <summary>GPU content transitions and an interruptible, velocity-preserving native window spring.</summary>
internal sealed class HaloMotion : IDisposable
{
    private readonly HaloWindowManager _window;
    private readonly Func<bool> _enabled;
    private readonly DispatcherQueueTimer _timer;
    private readonly SpringValue[] _spring;
    private HaloFrame _destination;
    private long _lastTick, _started;
    private Action? _completed;
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> _wired = new();
    public bool IsMoving => _timer.IsRunning;

    public HaloMotion(HaloWindowManager window, Func<bool> enabled)
    {
        _window = window; _enabled = enabled;
        var frame = window.CurrentFrame;
        _spring = [new(frame.X), new(frame.Y), new(frame.Width), new(frame.Height), new(frame.Radius), new(frame.DropAmount, .0005)];
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000d / 120);
        _timer.Tick += Tick;
    }
    public void Move(HaloFrame destination, bool animate, Action? completed = null)
    {
        _completed = completed; _destination = destination;
        var goals = new[] { destination.X, destination.Y, destination.Width, destination.Height, destination.Radius, destination.DropAmount };
        if (!animate || !_enabled())
        {
            _timer.Stop();
            for (var i = 0; i < _spring.Length; i++) _spring[i].Snap(goals[i]);
            _window.ApplyFrame(destination);
            Complete(); return;
        }
        if (!_timer.IsRunning)
        {
            var current = _window.CurrentFrame;
            var positions = new[] { current.X, current.Y, current.Width, current.Height, current.Radius, current.DropAmount };
            for (var i = 0; i < _spring.Length; i++) _spring[i].Snap(positions[i]);
        }
        for (var i = 0; i < _spring.Length; i++) _spring[i].Target = goals[i];
        _started = _lastTick = Stopwatch.GetTimestamp();
        _timer.Start();
    }
    private void Tick(DispatcherQueueTimer sender, object args)
    {
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds;
        _lastTick = now;
        for (var i = 0; i < _spring.Length; i++) _spring[i].Advance(seconds, i == 5 ? 30 : 22);
        _window.ApplyFrame(new(_spring[0].Value, _spring[1].Value, _spring[2].Value, _spring[3].Value, _spring[4].Value, _destination.Scale, _spring[5].Value));
        if (_spring.All(s => s.IsSettled) || Stopwatch.GetElapsedTime(_started, now).TotalSeconds > 1)
        {
            _timer.Stop(); _window.ApplyFrame(_destination); Complete();
        }
    }
    private void Complete() { var callback = _completed; _completed = null; callback?.Invoke(); }
    public void Stop() { _timer.Stop(); _completed = null; }
    public void Reveal(FrameworkElement element, int delay = 0, int duration = 180, float scale = 1)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity"); visual.StopAnimation("Scale");
        visual.Opacity = 1; visual.Scale = Vector3.One;
        if (!_enabled()) return;
        visual.CenterPoint = new((float)(element.ActualWidth / 2), (float)(element.ActualHeight / 2), 0);
        var compositor = visual.Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(new(.16f, 1), new(.3f, 1));
        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0); opacity.InsertKeyFrame(1, 1, ease);
        opacity.Duration = TimeSpan.FromMilliseconds(duration); opacity.DelayTime = TimeSpan.FromMilliseconds(delay);
        opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        var size = compositor.CreateVector3KeyFrameAnimation();
        size.InsertKeyFrame(0, new(scale, scale, 1)); size.InsertKeyFrame(1, Vector3.One, ease);
        size.Duration = opacity.Duration; size.DelayTime = opacity.DelayTime; size.DelayBehavior = opacity.DelayBehavior;
        visual.StartAnimation("Opacity", opacity); visual.StartAnimation("Scale", size);
    }
    public void Fade(FrameworkElement element, bool visible, int milliseconds = 100)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity");
        if (!_enabled()) { visual.Opacity = visible ? 1 : 0; return; }
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1, visible ? 1 : 0);
        animation.Duration = TimeSpan.FromMilliseconds(milliseconds);
        visual.StartAnimation("Opacity", animation);
    }
    public void Reset(FrameworkElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity"); visual.StopAnimation("Scale"); visual.Opacity = 1; visual.Scale = Vector3.One;
    }
    public void Wire(FrameworkElement element)
    {
        if (_wired.TryGetValue(element, out _)) return;
        _wired.Add(element, new object());
        element.PointerExited += (_, _) => Scale(element, 1);
        element.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => Scale(element, .965f)), true);
        element.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => Scale(element, 1)), true);
        element.PointerCaptureLost += (_, _) => Scale(element, 1);
    }
    private void Scale(FrameworkElement element, float value)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (!_enabled()) { visual.StopAnimation("Scale"); visual.Scale = Vector3.One; return; }
        visual.CenterPoint = new((float)(element.ActualWidth / 2), (float)(element.ActualHeight / 2), 0);
        // Keep each edge within one pixel of its layout slot, including wide settings tiles.
        var target = new Vector3(Math.Max(value, 1 - 2 / (float)Math.Max(1, element.ActualWidth)),
            Math.Max(value, 1 - 2 / (float)Math.Max(1, element.ActualHeight)), 1);
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, target);
        animation.Duration = TimeSpan.FromMilliseconds(120);
        visual.StartAnimation("Scale", animation);
    }
    public void Dispose() { Stop(); _timer.Tick -= Tick; }
}
