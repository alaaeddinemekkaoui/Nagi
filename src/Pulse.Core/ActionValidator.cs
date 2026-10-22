namespace Pulse.Core;

public static class ActionValidator
{
    private static readonly HashSet<string> UnsafeFiles = new(StringComparer.OrdinalIgnoreCase)
        { ".cmd", ".bat", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".hta", ".lnk", ".url", ".msi", ".reg", ".com", ".scr" };
    private static readonly HashSet<string> CommandHosts = new(StringComparer.OrdinalIgnoreCase)
        { "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe", "rundll32.exe", "regsvr32.exe" };
    public static OperationResult Validate(QuickAction action, PulseSettings? settings = null)
    {
        if (!Enum.IsDefined(action.Kind)) return OperationResult.Fail("Unknown action type.");
        if (string.IsNullOrWhiteSpace(action.Label)) return OperationResult.Fail("Give the action a name.");
        if (action.Kind == ActionKind.ToggleHalo) return OperationResult.Ok("Toggle Nagi");
        if (action.Kind == ActionKind.Profile)
            return Enum.TryParse<Profile>(action.Target, out var profile) && ProfileWorkspaces.IsKnown(profile, settings?.CustomProfiles)
                ? OperationResult.Ok("Switch profile") : OperationResult.Fail("Choose Work, Gaming or Focus.");
        if (action.Kind == ActionKind.Url)
            return Uri.TryCreate(action.Target, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
                && string.IsNullOrEmpty(uri.UserInfo) ? OperationResult.Ok("Open URL") : OperationResult.Fail("Use an http or https URL without credentials.");
        if (!Path.IsPathFullyQualified(action.Target) || action.Target.StartsWith("\\\\", StringComparison.Ordinal)
            || action.Target.Contains('"') || action.Target.Contains('\0') || action.Target.AsSpan(2).Contains(':'))
            return OperationResult.Fail("Choose an absolute local path without command arguments.");
        if (action.Kind == ActionKind.Folder)
            return Directory.Exists(action.Target) ? OperationResult.Ok("Open folder") : OperationResult.Fail("That folder does not exist.");
        if (!File.Exists(action.Target)) return OperationResult.Fail("That file does not exist.");
        var extension = Path.GetExtension(action.Target);
        if (UnsafeFiles.Contains(extension) || CommandHosts.Contains(Path.GetFileName(action.Target)))
            return OperationResult.Fail("Script files, command hosts and indirect shortcuts are not supported.");
        if (action.Kind == ActionKind.Application && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Choose an .exe application.");
        if (action.Kind == ActionKind.File && extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Use the Application action type for executable files.");
        return OperationResult.Ok("Validated");
    }
}
