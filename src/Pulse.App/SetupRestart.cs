using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private bool _setupRestarting;
    public void RestartWelcome()
    {
        if (_closed || _setupRestarting) return;
        if (_dialogOpen) { ViewModel.Publish("Close the current dialog, then choose Run welcome setup again.", true); return; }
        if (ViewModel.Settings.SchemaVersion > 1) { ViewModel.Publish("Setup is unavailable while newer settings are read-only.", true); return; }
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--setup"); start.ArgumentList.Add("--wait-for-exit"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            if (((App)Application.Current).LaunchOptions.DataDirectory is { } directory)
            { start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(directory); }
            using var child = Process.Start(start);
            if (child is null) throw new InvalidOperationException("Setup could not be opened.");
            _setupRestarting = true; Close();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { ViewModel.Publish("Could not open welcome setup: " + ex.Message, true); }
    }
}
