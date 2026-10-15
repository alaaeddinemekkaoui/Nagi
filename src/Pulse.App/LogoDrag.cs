using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Pulse.Core;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private DispatcherQueueTimer? _logoDragTimer;
    private bool _logoDragging, _suppressLogoClick;
    private int _logoDragGeneration;
    private double _logoScreenX, _logoScreenY;
    private HaloFrame _logoOrigin;

    // WinUI misses pointer events in a nonactivating window. Observe only the primary mouse
    // button while compact, then sample at 90 Hz for the duration of a logo press.
    private bool ObserveLogoDrag()
    {
        if (_logoDragTimer?.IsRunning == true) return true;
        if (_closed || _userHidden || _dock.Stage != DockStage.Compact || _quickMenuOpen || _tray?.IsMenuOpen == true
            || !_windowManager.IsPrimaryButtonPressed || !_windowManager.TryGetPointerPosition(out var x, out var y)) return false;
        var origin = CompactBrand.TransformToVisual(Root).TransformPoint(new(0, 0));
        if (x < origin.X || x > origin.X + CompactBrand.ActualWidth || y < origin.Y || y > origin.Y + CompactBrand.ActualHeight) return false;
        if (!_windowManager.TryGetPointerScreenPosition(out _logoScreenX, out _logoScreenY)) return false;
        _logoDragGeneration++; _suppressLogoClick = false;
        _logoOrigin = _windowManager.CurrentFrame;
        if (_logoDragTimer is null)
        {
            _logoDragTimer = DispatcherQueue.CreateTimer();
            _logoDragTimer.Interval = TimeSpan.FromMilliseconds(1000d / 90);
            _logoDragTimer.Tick += (_, _) => TrackLogoDrag();
        }
        _logoDragTimer.Start();
        return true;
    }

    private void TrackLogoDrag()
    {
        if (_closed || _userHidden || _dock.Stage != DockStage.Compact || !_windowManager.IsPrimaryButtonPressed)
        { _ = FinishLogoDragAsync(); return; }
        if (!_windowManager.TryGetPointerScreenPosition(out var x, out var y)) return;
        var dx = x - _logoScreenX; var dy = y - _logoScreenY;
        if (!_logoDragging)
        {
            if (Math.Abs(dx) < 6 * _logoOrigin.Scale && Math.Abs(dy) < 6 * _logoOrigin.Scale) return;
            _logoDragging = _suppressLogoClick = true;
            ResetPreview(); Layout(); _motion.Stop();
            _logoOrigin = _windowManager.CurrentFrame;
        }
        var current = _windowManager.CurrentFrame;
        var target = _logoOrigin with { X = _logoOrigin.X + dx, Y = _logoOrigin.Y + dy };
        if (Math.Abs(current.X - target.X) >= .5 || Math.Abs(current.Y - target.Y) >= .5)
            _windowManager.MoveDockDrag(target);
    }

    private async Task FinishLogoDragAsync()
    {
        _logoDragTimer?.Stop();
        var moved = _logoDragging; _logoDragging = false;
        if (!_closed)
        {
            if (moved && !_userHidden && _dock.Stage == DockStage.Compact) _windowManager.FinishDockDrag();
            else _windowManager.CancelDockDrag();
        }
        if (!moved) return;
        var generation = _logoDragGeneration;
        await Task.Delay(200);
        if (generation == _logoDragGeneration) _suppressLogoClick = false;
    }
}
