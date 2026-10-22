using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.App.Themes;
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
    private Color NotchColor
    {
        get
        {
            var color = AppAppearance.Surface(ViewModel.Settings, Shell.ActualTheme == ElementTheme.Light, false);
            if (ViewModel.Settings.LuaUiEnabled && _luaStyle.Surface is { } surface && AppAppearance.Tint(ViewModel.Settings) is null)
            { color = ParseLuaColor(surface); color.A = 255; }
            return color;
        }
    }
    private void NotchColorsChanged(UISettings sender, object args) => DispatcherQueue.TryEnqueue(() =>
    { if (!_closed) { ApplyGlass(); ApplyVisualizerAppearance(); } });

    private void BuildBackgroundAppearance(StackPanel panel)
    {
        if (ViewModel.Settings.Theme is not ("Dark" or "Light")) return;
        panel.Children.Add(Heading(L.T("Background & notch")));
        panel.Children.Add(Note(L.T("One color and material across the window, dock and notch.")));
        var enabled = new ToggleSwitch { Header = L.T("Tint background"), IsOn = ViewModel.Settings.NotchColorMode != "Theme", OnContent = L.T("On"), OffContent = L.T("Off") };
        AutomationProperties.SetName(enabled, L.T("Tint background"));
        enabled.Toggled += async (_, _) =>
        { if (await CommitAsync(s => s with { NotchColorMode = enabled.IsOn ? "System" : "Theme" })) BuildSettingsPage(); };
        panel.Children.Add(enabled);
        if (ViewModel.Settings.NotchColorMode == "Theme") return;
        var mode = Select(new[] { "System", "Custom" }, ViewModel.Settings.NotchColorMode, "Color source");
        AutomationProperties.SetAutomationId(mode, "BackgroundColorMode");
        mode.SelectionChanged += async (_, _) =>
        { if (mode.SelectedItem is string value && await CommitAsync(s => s with { NotchColorMode = value })) BuildSettingsPage(); };
        panel.Children.Add(Labeled(L.T("Color source"), mode));
        if (ViewModel.Settings.NotchColorMode != "Custom") return;
        var choose = Button(L.T("Choose background color"), async (_, _) =>
        {
            var picker = new ColorPicker { Color = AppAppearance.Tint(ViewModel.Settings) ?? NotchColor, IsAlphaEnabled = false, IsHexInputVisible = true, MinWidth = 280 };
            var dialog = Dialog(L.T("Background color"), picker);
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
        AutomationProperties.SetAutomationId(choose, "ChooseBackgroundColor"); panel.Children.Add(choose);
    }
}
