using Microsoft.UI.Xaml;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private DockIntroductionWindow? _introduction;
    internal void BeginDockIntroduction()
    {
        if (_closed) return;
        DismissDockIntroduction(); ResetPreview(); _controlCenter = false; _dock.Collapse();
        Layout(); _windowManager.SetVisible(true);
        try
        {
            _introduction = new(_windowManager.CurrentFrame, ViewModel.Settings.Position.Anchor, Shell.ActualTheme, () => _introduction = null, ViewModel.Settings);
            _introduction.ShowIntroduction();
        }
        catch (Exception ex) { DismissDockIntroduction(); _log.Write("introduction", "Could not show dock introduction", ex); }
    }
    private void DismissDockIntroduction() { var intro = _introduction; _introduction = null; intro?.Close(); }
}
