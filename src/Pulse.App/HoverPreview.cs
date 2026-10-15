using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private enum PreviewKind { None, System, Media }
    private PreviewKind _preview, _pendingPreview;
    private FrameworkElement? _pendingPreviewTarget;
    private bool _previewSuppressed;
    private double _dismissPointerX, _dismissPointerY;
    private readonly DispatcherTimer _previewOpen = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private readonly DispatcherTimer _previewClose = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _previewWatch = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool PreviewVisible => _dock.Stage == DockStage.Compact && _preview != PreviewKind.None;

    private void InitializePreviews()
    {
        _previewOpen.Tick += (_, _) =>
        {
            _previewOpen.Stop();
            if (_closed || _dock.Stage != DockStage.Compact || _userHidden || _dialogOpen || _quickMenuOpen || _tray?.IsMenuOpen == true || !_windowManager.IsVisible) return;
            if (_previewSuppressed) return;
            if (_pendingPreviewTarget is null || !PointerOver(_pendingPreviewTarget)) return;
            _preview = _pendingPreview;
            PeekHeading.Text = _preview == PreviewKind.Media ? "Media & audio" : "System at a glance";
            SystemPeek.Visibility = _preview == PreviewKind.System ? Visibility.Visible : Visibility.Collapsed;
            MediaPeek.Visibility = _preview == PreviewKind.Media ? Visibility.Visible : Visibility.Collapsed;
            Layout(true); // Preserve the compact strip's screen position; never activate on hover.
        };
        _previewClose.Tick += (_, _) =>
        {
            if (_quickMenuOpen || _tray?.IsMenuOpen == true) { _previewClose.Stop(); return; }
            if (!PointerInsideHalo())
            {
                if (_dock.Stage == DockStage.Compact && !_dock.Pinned) Collapse(); else ClosePreview();
            }
            else _previewClose.Stop();
        };
        // Native pointer sampling also covers nonactivating compact windows, where XAML can
        // miss enter/exit notifications. No layout/paint occurs until the hover state changes.
        _previewWatch.Tick += (_, _) =>
        {
            if (_closed || _expanded || _userHidden || _dialogOpen || _quickMenuOpen || _tray?.IsMenuOpen == true || !_windowManager.IsVisible) return;
            if (ObserveDockPointer()) { _previewOpen.Stop(); return; }
            if (_previewSuppressed)
            {
                if (_motion.IsMoving) return;
                if (_windowManager.TryGetPointerScreenPosition(out var x, out var y)
                    && (Math.Abs(x - _dismissPointerX) > 4 || Math.Abs(y - _dismissPointerY) > 4)
                    && !PointerOver(CompactCpuButton) && !PointerOver(CompactMediaButton)) _previewSuppressed = false;
                return;
            }
            if (PointerOver(CompactCpuButton)) QueuePreview(PreviewKind.System, CompactCpuButton);
            else if (PointerOver(RailGpuButton)) QueuePreview(PreviewKind.System, RailGpuButton);
            else if (PointerOver(CompactMediaButton)) QueuePreview(
                PreviewKind.Media, CompactMediaButton);
            else _previewOpen.Stop();
            if (PointerInsideHalo()) _previewClose.Stop();
            else if (PreviewVisible && !_previewClose.IsEnabled) _previewClose.Start();
        };
    }

    private void QueuePreview(PreviewKind kind, FrameworkElement target)
    {
        _previewClose.Stop();
        if (_closed || _dock.Stage != DockStage.Compact || _userHidden || _dialogOpen || _quickMenuOpen || _previewSuppressed || _motion.IsMoving || PointerOnEdgeLip()) return;
        if (_previewOpen.IsEnabled && _pendingPreview == kind && ReferenceEquals(_pendingPreviewTarget, target)) return;
        _pendingPreview = kind;
        _pendingPreviewTarget = target;
        _previewOpen.Stop();
        if (_preview != kind) _previewOpen.Start();
    }

    private void CpuPreview_Entered(object sender, PointerRoutedEventArgs e) => QueuePreview(PreviewKind.System, sender as FrameworkElement ?? CompactCpuButton);
    private void MediaPreview_Entered(object sender, PointerRoutedEventArgs e) => QueuePreview(
        PreviewKind.Media, CompactMediaButton);
    private void PreviewRoot_Entered(object sender, PointerRoutedEventArgs e) => _previewClose.Stop();
    private void PreviewRoot_Exited(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Root).Position;
        // Ignore descendant exits and exits generated while the native bounds spring resizes.
        if (point.X >= 0 && point.Y >= 0 && point.X < Root.ActualWidth && point.Y < Root.ActualHeight) return;
        _previewOpen.Stop();
        if (PreviewVisible || (_dock.Stage == DockStage.Compact && !_dock.Pinned)) { _previewClose.Stop(); _previewClose.Start(); }
    }

    private void ClosePreview()
    {
        _previewOpen.Stop(); _previewClose.Stop();
        if (_preview == PreviewKind.None) return;
        _preview = _pendingPreview = PreviewKind.None;
        if (!_closed && !_expanded && !_userHidden) Layout(true);
    }
    private void ResetPreview()
    {
        // A resizing client area can report a stationary cursor outside its old XAML bounds.
        // Require a real pointer departure before allowing an explicitly dismissed preview back.
        _previewSuppressed = !_closed && !_expanded && (PointerOver(CompactCpuButton) || PointerOver(CompactMediaButton))
            && _windowManager.TryGetPointerScreenPosition(out _dismissPointerX, out _dismissPointerY);
        _previewOpen.Stop(); _previewClose.Stop();
        _preview = _pendingPreview = PreviewKind.None;
        _pendingPreviewTarget = null;
        PeekHost.Visibility = Visibility.Collapsed;
    }
    private bool PointerOver(FrameworkElement target)
    {
        if (!target.IsLoaded || !Root.IsLoaded) return false;
        if (!_windowManager.TryGetPointerPosition(out var x, out var y)) return false;
        var origin = target.TransformToVisual(Shell).TransformPoint(new(0, 0));
        return x >= origin.X && y >= origin.Y && x < origin.X + target.ActualWidth && y < origin.Y + target.ActualHeight;
    }
    private bool PointerInsideHalo()
    {
        if (!_windowManager.TryGetPointerPosition(out var x, out var y)) return false;
        var frame = _windowManager.CurrentFrame;
        return x >= 0 && y >= 0 && x < frame.Width / frame.Scale && y < frame.Height / frame.Scale;
    }
    private void ClosePreview_Click(object sender, RoutedEventArgs e) => ClosePreview();
    private void PreviewMore_Click(object sender, RoutedEventArgs e)
    {
        var media = _preview == PreviewKind.Media;
        Navigate("Overview"); Reveal();
        if (media) MediaCard.StartBringIntoView();
    }
}
