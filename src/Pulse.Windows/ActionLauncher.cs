using System.Diagnostics;
using Microsoft.Win32;
using Pulse.Core;

namespace Pulse.Windows;

public static class ActionLauncher
{
    public static OperationResult Launch(QuickAction action)
    {
        var validation = ActionValidator.Validate(action);
        if (!validation.Success) return validation;
        if (action.Kind is ActionKind.Profile or ActionKind.ToggleHalo) return OperationResult.Fail("This action must be handled by Nagi.");
        try
        {
            Process.Start(new ProcessStartInfo(action.Target) { UseShellExecute = true });
            return OperationResult.Ok("Opened " + action.Label);
        }
        catch (Exception ex) { return OperationResult.Fail("Could not open " + action.Label + ": " + ex.GetType().Name); }
    }
    public static OperationResult ConfigureStartup(bool enabled, string executable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (enabled) key.SetValue("Nagi", "\"" + executable + "\""); else key.DeleteValue("Nagi", false);
            return OperationResult.Ok(enabled ? "Nagi will start when you sign in." : "Startup disabled.");
        }
        catch (Exception ex) { return OperationResult.Fail("Startup registration failed: " + ex.GetType().Name); }
    }
}
