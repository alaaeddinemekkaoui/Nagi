using System.Text.Json;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class DiagnosticLogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NagiLogs", Guid.NewGuid().ToString("N"));
    [Fact]
    public void ActivityAndErrorsHaveSeparateFilesAndExceptionsKeepTheirStack()
    {
        var log = new DiagnosticLog(_root);
        log.Write("lifecycle", "Started");
        Assert.True(File.Exists(log.ErrorFilePath));
        Assert.Empty(File.ReadAllLines(log.ErrorFilePath));
        try { throw new InvalidOperationException("Failure details"); }
        catch (Exception ex) { log.Write("updates", "Download failed", ex); }
        log.Write("operation", "App not installed");
        Assert.Equal(3, File.ReadAllLines(log.FilePath).Length);
        var errors = File.ReadAllLines(log.ErrorFilePath);
        Assert.Equal(2, errors.Length);
        using var entry = JsonDocument.Parse(errors[0]);
        Assert.Equal("error", entry.RootElement.GetProperty("level").GetString());
        Assert.Contains("Failure details", entry.RootElement.GetProperty("exception").GetString());
        Assert.Contains(nameof(ActivityAndErrorsHaveSeparateFilesAndExceptionsKeepTheirStack), entry.RootElement.GetProperty("exception").GetString());
    }
    [Fact]
    public void ActivityRotationAndAnUnavailableActivityFileDoNotLoseTheError()
    {
        var log = new DiagnosticLog(_root); log.Write("lifecycle", "Started");
        File.WriteAllText(log.FilePath, new string('x', 1_000_001));
        log.Write("lifecycle", "New activity");
        Assert.True(File.Exists(log.FilePath + ".previous"));
        Assert.False(File.Exists(log.ErrorFilePath + ".previous"));
        using var locked = File.Open(log.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        log.Write("fatal", "Fatal with activity file locked");
        Assert.Contains("Fatal with activity file locked", File.ReadAllText(log.ErrorFilePath));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
