using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly SolidColorBrush _visualizerColor = new();
    private Controls.MusicVisualizer? _visualizerSettingsPreview;
    private SolidColorBrush VisualizerBrush
    {
        get
        {
            var color = _themeAccessibility.HighContrast ? ((SolidColorBrush)ThemeBrush("PulseAccentBrush")).Color
                : ViewModel.Settings.VisualizerColorMode == "Custom"
                    ? Color.FromArgb(255, Convert.ToByte(ViewModel.Settings.VisualizerColor.Substring(1, 2), 16),
                        Convert.ToByte(ViewModel.Settings.VisualizerColor.Substring(3, 2), 16), Convert.ToByte(ViewModel.Settings.VisualizerColor.Substring(5, 2), 16))
                    : _notchSystemColors.GetColorValue(UIColorType.Accent);
            if (!_visualizerColor.Color.Equals(color)) _visualizerColor.Color = color;
            return _visualizerColor;
        }
    }
    private void ApplyVisualizerAppearance()
    {
        var color = VisualizerBrush;
        _restingVisualizer.SetColor(color); PeekMusicVisualizer.SetColor(color); OutputMusicVisualizer.SetColor(color);
        CompactMusicVisualizer.SetColor(color); _visualizerSettingsPreview?.SetColor(color);
    }
    private void BuildVisualizerSettings()
    {
        var content = Stack(); content.Children.Add(Heading(L.T("Audio visualizer")));
        content.Children.Add(Note(L.T("Rounded audio bars use the Windows accent color or your own color across the notch and media panels.")));
        var mode = Select(["System", "Custom"], ViewModel.Settings.VisualizerColorMode, "Visualizer color source");
        AutomationProperties.SetAutomationId(mode, "VisualizerColorMode");
        mode.SelectionChanged += async (_, _) =>
        {
            if (mode.SelectedItem is string selected && await CommitAsync(s => s with { VisualizerColorMode = selected })) BuildSettingsPage();
        };
        content.Children.Add(Labeled(L.T("Color source"), mode));
        if (ViewModel.Settings.VisualizerColorMode == "Custom")
        {
            var choose = Button(L.T("Choose visualizer color"), async (_, _) =>
            {
                var picker = new ColorPicker { Color = VisualizerBrush.Color, IsAlphaEnabled = false, IsHexInputVisible = true, MinWidth = 280 };
                AutomationProperties.SetAutomationId(picker, "VisualizerColorPicker");
                var dialog = Dialog(L.T("Visualizer color"), picker);
                dialog.PrimaryButtonClick += async (_, args) =>
                {
                    var deferral = args.GetDeferral();
                    try
                    {
                        var color = picker.Color;
                        if (!await CommitAsync(s => s with { VisualizerColorMode = "Custom", VisualizerColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}" })) args.Cancel = true;
                    }
                    finally { deferral.Complete(); }
                };
                await ShowDialogAsync(dialog); BuildSettingsPage();
            });
            AutomationProperties.SetAutomationId(choose, "ChooseVisualizerColor"); content.Children.Add(choose);
        }
        var preview = new Controls.MusicVisualizer(5) { Width = 92, Height = 24, HorizontalAlignment = HorizontalAlignment.Left };
        _visualizerSettingsPreview = preview;
        AutomationProperties.SetAutomationId(preview, "VisualizerColorPreview");
        preview.Update(.3, true, VisualizerBrush, false);
        AutomationProperties.SetName(preview, L.T("Visualizer color")); content.Children.Add(preview);
        SettingsPage.Children.Add(Card(content));
    }
}
