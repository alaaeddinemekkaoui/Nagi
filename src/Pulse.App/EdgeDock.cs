using System.Diagnostics;
using Microsoft.UI.Xaml;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private long _dockEnteredAt;
    private bool _dockSuppressed;
    private double _dockDismissX, _dockDismissY;
    private bool _dockAwaitPointerMove;
    private double _dockRevealX, _dockRevealY;

    private bool ObserveDockPointer()
    {
        if (_dockSuppressed && _dock.Stage == DockStage.Resting
            && _windowManager.TryGetPointerScreenPosition(out var pointerX, out var pointerY)
            && (Math.Abs(pointerX - _dockDismissX) > 4 || Math.Abs(pointerY - _dockDismissY) > 4)
            && !PointerInsideHalo()) _dockSuppressed = false;
        if (_motion.IsMoving) return true;
        var inside = PointerInsideHalo();
        if (_dock.Stage == DockStage.Resting)
        {
            _dockAwaitPointerMove = false;
            if (_dockSuppressed)
            {
                if (!inside) _dockSuppressed = false;
                _dockEnteredAt = 0; return true;
            }
            if (!inside) _dockEnteredAt = 0;
            else if (_dockEnteredAt == 0) _dockEnteredAt = Stopwatch.GetTimestamp();
            else if (Stopwatch.GetElapsedTime(_dockEnteredAt).TotalMilliseconds >= 180)
            {
                RememberDockRevealPointer();
                _dockEnteredAt = 0; _dock.Hover(); _previewSuppressed = false; Layout(true);
                DismissDockIntroduction();
            }
            return true;
        }
        if (!inside)
        {
            if ((!_dock.AlwaysShowMiniBar || PreviewVisible) && !_previewClose.IsEnabled) _previewClose.Start();
            return true;
        }
        if (inside) _previewClose.Stop();
        // Revealing the compact view must not immediately open the media card under the old tab.
        return inside && PointerOnEdgeLip();
    }
    private bool PointerOnEdgeLip()
    {
        if (_dockAwaitPointerMove && _windowManager.TryGetPointerScreenPosition(out var screenX, out var screenY))
        {
            if (Math.Abs(screenX - _dockRevealX) <= 4 && Math.Abs(screenY - _dockRevealY) <= 4) return true;
            _dockAwaitPointerMove = false;
        }
        if (!_windowManager.TryGetPointerPosition(out var x, out var y)) return false;
        var frame = _windowManager.CurrentFrame;
        if (ViewModel.Settings.Position.Orientation == "Horizontal")
            return ViewModel.Settings.Position.Anchor == "Top edge" ? y < 12 : y > frame.Height / frame.Scale - 12;
        return ViewModel.Settings.Position.Anchor == "Left edge" ? x < 12 : x > frame.Width / frame.Scale - 12;
    }
    private void RememberDockRevealPointer() => _dockAwaitPointerMove =
        _windowManager.TryGetPointerScreenPosition(out _dockRevealX, out _dockRevealY);
    private void Dock_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _userHidden) return;
        DismissDockIntroduction();
        if (ReferenceEquals(sender, CompactBrand)) { OpenControlCenter("Profiles"); return; }
        _dockSuppressed = _previewSuppressed = false; _dockEnteredAt = 0;
        _previewOpen.Stop(); _previewClose.Stop();
        if (_dock.Stage == DockStage.Resting) RememberDockRevealPointer();
        _dock.Click();
        if (_expanded) { Navigate("Overview"); Reveal(); }
        else Layout(true);
    }
}
