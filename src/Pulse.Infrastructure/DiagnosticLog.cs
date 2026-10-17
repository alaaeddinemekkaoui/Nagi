using System.Text.Json;
using Pulse.Core;

namespace Pulse.Infrastructure;

public sealed class DiagnosticLog(string? directory = null) : IDiagnosticLog
{
    private readonly object _sync = new();
    public string FilePath { get; } = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse"), "logs", "pulse.jsonl");
    public string ErrorFilePath => Path.Combine(Path.GetDirectoryName(FilePath)!, "errors.jsonl");
    public void Write(string category, string message, Exception? exception = null)
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (!File.Exists(ErrorFilePath)) File.AppendAllText(ErrorFilePath, "");
                var failure = exception is not null || category is "fatal" or "error" or "operation";
                var entry = JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, level = failure ? "error" : "info", category, message,
                    version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(), processId = Environment.ProcessId,
                    error = exception?.GetType().FullName, code = exception?.HResult, exception = exception?.ToString() }) + Environment.NewLine;
                Append(FilePath, entry);
                if (failure) Append(ErrorFilePath, entry);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static void Append(string path, string entry)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, entry);
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
