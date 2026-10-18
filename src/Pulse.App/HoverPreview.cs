using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private enum PreviewKind { None, System, Media, Obs, Apps }
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
            ShowPreview(_pendingPreview);
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
                    && !PointerOver(CompactCpuButton) && !PointerOver(RailGpuButton) && !PointerOver(CompactMediaButton) && !PointerOver(RailObsButton) && !PointerOver(AppFolderButton)) _previewSuppressed = false;
                return;
            }
            if (PointerOver(CompactCpuButton)) QueuePreview(PreviewKind.System, CompactCpuButton);
            else if (PointerOver(RailGpuButton)) QueuePreview(PreviewKind.System, RailGpuButton);
            else if (PointerOver(CompactMediaButton)) QueuePreview(
                PreviewKind.Media, CompactMediaButton);
            else if (PointerOver(RailObsButton) && ViewModel.Settings.Workspace(ViewModel.Profile).ObsPresentation == "Widget") QueuePreview(PreviewKind.Obs, RailObsButton);
            else if (PointerOver(AppFolderButton)) QueuePreview(PreviewKind.Apps, AppFolderButton);
            else _previewOpen.Stop();
            if (PointerInsideHalo()) _previewClose.Stop();
            else if (PreviewVisible && !_previewClose.IsEnabled) _previewClose.Start();
        };
    }

    private void ShowPreview(PreviewKind kind)
    {
        var switching = PreviewVisible;
        _preview = kind;
        PeekHeading.Text = L.T(kind switch { PreviewKind.Apps => "More apps", PreviewKind.Obs => "OBS Studio", PreviewKind.Media => "Media & audio", _ => "System at a glance" });
        SystemPeek.Visibility = kind == PreviewKind.System ? Visibility.Visible : Visibility.Collapsed;
        MediaPeek.Visibility = kind == PreviewKind.Media ? Visibility.Visible : Visibility.Collapsed;
        ObsPeek.Visibility = kind == PreviewKind.Obs ? Visibility.Visible : Visibility.Collapsed;
        AppsPeek.Visibility = kind == PreviewKind.Apps ? Visibility.Visible : Visibility.Collapsed;
        PeekSpeakersButton.Visibility = kind == PreviewKind.Media ? Visibility.Visible : Visibility.Collapsed;
        Layout(true); // Hover preserves the dock's screen position without taking focus.
        if (switching) _motion.Reveal(kind switch { PreviewKind.Apps => AppsPeek, PreviewKind.Obs => ObsPeek,
            PreviewKind.Media => MediaPeek, _ => SystemPeek }, 0, 160, 1);
    }
    private void AppFolder_Entered(object sender, PointerRoutedEventArgs e) => QueuePreview(PreviewKind.Apps, AppFolderButton);
    private void AppFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _userHidden || _dialogOpen || AppFolderButton.Visibility != Visibility.Visible) return;
        if (_expanded) { Navigate("Overview"); WorkspaceAppsCard.StartBringIntoView(); return; }
        _previewOpen.Stop(); _previewClose.Stop(); _previewSuppressed = false;
        ShowPreview(PreviewKind.Apps);
    }

    private void QueuePreview(PreviewKind kind, FrameworkElement target)
    {
        _previewClose.Stop();
        if (kind == PreviewKind.Obs && ViewModel.Settings.Workspace(ViewModel.Profile).ObsPresentation != "Widget") return;
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
    private void ObsPreview_Entered(object sender, PointerRoutedEventArgs e) => QueuePreview(PreviewKind.Obs, RailObsButton);
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
        _previewSuppressed = !_closed && !_expanded && (PointerOver(CompactCpuButton) || PointerOver(RailGpuButton) || PointerOver(CompactMediaButton) || PointerOver(RailObsButton) || PointerOver(AppFolderButton))
            && _windowManager.TryGetPointerScreenPosition(out _dismissPointerX, out _dismissPointerY);
        _previewOpen.Stop(); _previewClose.Stop();
        _preview = _pendingPreview = PreviewKind.None;
        _pendingPreviewTarget = null;
        PeekHost.Visibility = Visibility.Collapsed;
    }
    private bool PointerOver(FrameworkElement target)
    {
        if (!target.IsLoaded || !Root.IsLoaded || target.Visibility != Visibility.Visible) return false;
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
        var apps = _preview == PreviewKind.Apps;
        OpenControlCenter("Overview");
        if (media) MediaCard.StartBringIntoView();
        if (apps) WorkspaceAppsCard.StartBringIntoView();
    }
}
