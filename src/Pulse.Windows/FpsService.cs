using System.Diagnostics;
using Pulse.Core;

namespace Pulse.Windows;

public sealed class FpsService(string executable, string? externalCsvPath = null)
{
    private readonly string _externalCsv = externalCsvPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse", "Telemetry", "foreground-presents.csv");
    private long _retryAfter;
    private string _retryStatus = "PresentMon needs tracing permission · see Settings";
    public async Task<FpsSnapshot> SampleAsync(bool enabled, int processId, CancellationToken token)
    {
        if (!enabled) return new(null, processId, "FPS capture off · enable in Settings");
        if (processId <= 0 || processId == Environment.ProcessId) return new(null, processId, "Select a game or graphics app");
        try
        {
            if (File.Exists(_externalCsv) && DateTime.UtcNow - File.GetLastWriteTimeUtc(_externalCsv) < TimeSpan.FromSeconds(8) && new FileInfo(_externalCsv).Length <= 4 * 1024 * 1024)
            {
                await using var file = new FileStream(_externalCsv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(file);
                var csv = await reader.ReadToEndAsync(token);
                var external = IntegrationMath.FpsFromCsv(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries), processId);
                return new(external, processId, external.HasValue ? "PresentMon helper · application presents/s" : "Select the game captured by the PresentMon helper");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (!File.Exists(executable)) return new(null, processId, "PresentMon is missing from this package");
        if (Environment.TickCount64 < _retryAfter) return new(null, processId, _retryStatus);
        var session = "Pulse-FPS-" + Guid.NewGuid().ToString("N");
        using var process = new Process { StartInfo = new(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var value in new[] { "--process_id", processId.ToString(System.Globalization.CultureInfo.InvariantCulture), "--session_name", session,
            "--output_stdout", "--v1_metrics", "--no_console_stats", "--no_track_input", "--timed", "2", "--terminate_after_timed", "--terminate_on_proc_exit" }) process.StartInfo.ArgumentList.Add(value);
        try
        {
            token.ThrowIfCancellationRequested(); process.Start();
            var stdout = ReadBoundedAsync(process.StandardOutput); var stderr = ReadBoundedAsync(process.StandardError);
            // The owned timed trace normally exits itself in two seconds. Give it a bounded grace.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                await StopOwnedSessionAsync(session);
                if (!process.HasExited && !process.WaitForExit(1000)) process.Kill();
                token.ThrowIfCancellationRequested(); return new(null, processId, "FPS trace timed out");
            }
            var text = await stdout; var error = await stderr;
            if (process.ExitCode != 0)
            {
                _retryAfter = Environment.TickCount64 + 30000;
                _retryStatus = error.Contains("access denied", StringComparison.OrdinalIgnoreCase)
                    ? "Windows denied FPS tracing · start the FPS helper in Integrations"
                    : "PresentMon could not trace · permission or unsupported target";
                return new(null, processId, _retryStatus);
            }
            var fps = IntegrationMath.FpsFromCsv(text.Split('\n', StringSplitOptions.RemoveEmptyEntries), processId);
            return new(fps, processId, fps.HasValue ? "PresentMon · application presents/s" : "No present events from the foreground app");
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return new(null, processId, "PresentMon could not start"); }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var builder = new System.Text.StringBuilder(); var buffer = new char[4096]; int read;
        while ((read = await reader.ReadAsync(buffer)) > 0) if (builder.Length < 4 * 1024 * 1024) builder.Append(buffer, 0, Math.Min(read, 4 * 1024 * 1024 - builder.Length));
        return builder.ToString();
    }
    private async Task StopOwnedSessionAsync(string session)
    {
        try
        {
            using var stop = new Process { StartInfo = new(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden } };
            foreach (var value in new[] { "--session_name", session, "--terminate_existing_session" }) stop.StartInfo.ArgumentList.Add(value);
            stop.Start(); using var timeout = new CancellationTokenSource(1500); await stop.WaitForExitAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException) { }
    }
}
