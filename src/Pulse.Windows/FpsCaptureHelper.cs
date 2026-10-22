using System.Diagnostics;
using System.Globalization;
using Pulse.Core;

namespace Pulse.Windows;

public static class FpsCaptureHelper
{
    public static ProcessStartInfo CreateStartInfo(string helper, string settingsFile, int gameProcessId, int ownerProcessId)
    {
        if (gameProcessId <= 0 || ownerProcessId <= 0 || gameProcessId == ownerProcessId) throw new ArgumentException("Choose a game or graphics application first.");
        if (!Path.IsPathFullyQualified(helper) || !File.Exists(helper) || !Path.IsPathFullyQualified(settingsFile)) throw new ArgumentException("The FPS helper is missing from this package.");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", helper, "-GameProcessId", gameProcessId.ToString(CultureInfo.InvariantCulture),
            "-DataDirectory", Path.Combine(Path.GetDirectoryName(settingsFile)!, "Telemetry"), "-OwnerProcessId", ownerProcessId.ToString(CultureInfo.InvariantCulture), "-SettingsPath", settingsFile })
            start.ArgumentList.Add(argument);
        return start;
    }
    public static OperationResult Start(string helper, string settingsFile, int gameProcessId, int ownerProcessId)
    {
        try { using var process = Process.Start(CreateStartInfo(helper, settingsFile, gameProcessId, ownerProcessId)); return process is null ? OperationResult.Fail("The FPS helper could not start.") : OperationResult.Ok("FPS helper started. Disable FPS or close Nagi to stop it."); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return OperationResult.Fail("FPS permission request cancelled."); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return OperationResult.Fail(ex.Message); }
    }
}
