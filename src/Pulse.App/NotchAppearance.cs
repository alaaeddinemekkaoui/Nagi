using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly UISettings _notchSystemColors = new();
    private bool _materialResting;
    private bool _hasPresentedFrame;
    private bool SettledNotch => _dock.Stage == Pulse.Core.DockStage.Resting && !_motion.IsMoving
        && (!_hasPresentedFrame || _windowManager.CurrentFrame.DropAmount >= .999);
    private Color NotchColor => ViewModel.Settings.NotchColorMode switch
    {
        "System" => _notchSystemColors.GetColorValue(UIColorType.Accent),
        "Custom" => Color.FromArgb(255, Convert.ToByte(ViewModel.Settings.NotchColor.Substring(1, 2), 16),
            Convert.ToByte(ViewModel.Settings.NotchColor.Substring(3, 2), 16), Convert.ToByte(ViewModel.Settings.NotchColor.Substring(5, 2), 16)),
        _ => Color.FromArgb(255, ((SolidColorBrush)ThemeBrush("SurfaceBrush")).Color.R,
            ((SolidColorBrush)ThemeBrush("SurfaceBrush")).Color.G, ((SolidColorBrush)ThemeBrush("SurfaceBrush")).Color.B)
    };
    private void NotchColorsChanged(UISettings sender, object args) => DispatcherQueue.TryEnqueue(() =>
    { if (!_closed) UpdateSurfaceMaterial(); });
    private void BuildNotchAppearance(StackPanel panel)
    {
        var mode = Select(new[] { "Theme", "System", "Custom" }, ViewModel.Settings.NotchColorMode, "Notch color");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(mode, "NotchColorMode");
        mode.SelectionChanged += async (_, _) =>
        { if (mode.SelectedItem is string value) await CommitAsync(s => s with { NotchColorMode = value }); };
        panel.Children.Add(Labeled(L.T("Notch background color"), mode));
        panel.Children.Add(Note(L.T("Theme uses the current appearance. System follows your Windows accent. Custom uses your chosen color.")));
        var choose = Button(L.T("Choose notch color"), async (_, _) =>
        {
            var picker = new ColorPicker { Color = NotchColor, IsAlphaEnabled = false, IsHexInputVisible = true, MinWidth = 280 };
            var dialog = Dialog(L.T("Notch background color"), picker);
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var defer = args.GetDeferral();
                try
                {
                    var color = picker.Color;
                    if (!await CommitAsync(s => s with { NotchColorMode = "Custom", NotchColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}" })) args.Cancel = true;
                }
                finally { defer.Complete(); }
            };
            await ShowDialogAsync(dialog); BuildSettingsPage();
        });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(choose, "ChooseNotchColor"); panel.Children.Add(choose);
    }
}
