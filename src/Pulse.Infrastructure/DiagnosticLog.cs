using System.Text.Json;
using Pulse.Core;

namespace Pulse.Infrastructure;

public sealed class DiagnosticLog(string? directory = null) : IDiagnosticLog
{
    private readonly object _sync = new();
    public string FilePath { get; } = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse"), "logs", "pulse.jsonl");
    public void Write(string category, string message, Exception? exception = null)
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Move(FilePath, FilePath + ".previous", true);
                File.AppendAllText(FilePath, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, category, message,
                    error = exception?.GetType().Name, code = exception?.HResult }) + Environment.NewLine);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
