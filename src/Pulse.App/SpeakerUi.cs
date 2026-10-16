using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Windows;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private async void Speakers_Click(object sender, RoutedEventArgs args)
    {
        if (_closed || _dialogOpen) return;
        // Dialogs need the expanded host; prevent an unpinned preview from collapsing underneath.
        if (!_expanded) { Navigate("Overview"); Reveal(); }
        var devices = new ListBox { SelectionMode = SelectionMode.Single, MaxHeight = 240 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(devices, "SpeakerDevices");
        var status = Note("Choose the Windows speaker or headphones used for media and system audio. Apps with their own output selection may keep that selection.");
        var body = Stack(); body.Children.Add(status); body.Children.Add(devices);
        var windowsSound = new Button { Content = "Windows sound settings" };
        windowsSound.Click += async (_, _) => await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:sound"));
        body.Children.Add(windowsSound);
        var dialog = Dialog("Speakers", body); dialog.PrimaryButtonText = "Use speaker";
        dialog.IsPrimaryButtonEnabled = false;
        async Task RefreshAsync()
        {
            try
            {
                var outputs = await Task.Run(_audio.GetOutputDevices);
                if (_closed) return;
                devices.Items.Clear();
                foreach (var output in outputs) devices.Items.Add(new ListBoxItem { Content = output.Name + (output.IsDefault ? " · current" : ""), Tag = output });
                devices.SelectedItem = devices.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((AudioOutputDevice)i.Tag).IsDefault);
                if (outputs.Count == 0) status.Text = "No speakers or headphones are connected. Connect an output, then reopen Speakers.";
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or InvalidCastException)
            { status.Text = "Speaker list unavailable. Open Windows sound settings to select an output."; }
        }
        devices.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = devices.SelectedItem is ListBoxItem;
        dialog.PrimaryButtonClick += async (_, click) =>
        {
            if (devices.SelectedItem is not ListBoxItem { Tag: AudioOutputDevice output }) { click.Cancel = true; return; }
            var deferral = click.GetDeferral(); dialog.IsPrimaryButtonEnabled = false;
            try
            {
                var result = await Task.Run(() => _audio.SetOutputDevice(output.Id));
                ViewModel.Publish(result.Message, true);
                if (!result.Success) { click.Cancel = true; status.Text = result.Message; await RefreshAsync(); }
            }
            finally { dialog.IsPrimaryButtonEnabled = devices.SelectedItem is ListBoxItem; deferral.Complete(); }
        };
        dialog.Opened += async (_, _) => await RefreshAsync();
        await ShowDialogAsync(dialog);
    }
}
