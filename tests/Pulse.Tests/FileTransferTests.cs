using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class FileTransferTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NagiTransferTests", Guid.NewGuid().ToString("N"));
    public FileTransferTests() => Directory.CreateDirectory(_directory);
    [Fact] public async Task CopyPreservesOriginalAndExistingDestinationWithUnicodeName()
    {
        var source = Path.Combine(_directory, "凪 note.txt"); await File.WriteAllTextAsync(source, "hello");
        var destination = Directory.CreateDirectory(Path.Combine(_directory, "out")).FullName;
        await File.WriteAllTextAsync(Path.Combine(destination, "凪 note.txt"), "existing");
        var result = await new FileTransferService().CopyAsync([source, source], destination, null, CancellationToken.None);
        Assert.Single(result); Assert.Equal("hello", await File.ReadAllTextAsync(source));
        Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(destination, "凪 note.txt")));
        Assert.Equal("hello", await File.ReadAllTextAsync(result[0])); Assert.EndsWith("凪 note (1).txt", result[0]);
    }
    [Fact] public async Task CancellationDeletesPartialOutputAndKeepsSource()
    {
        var source = Path.Combine(_directory, "large.bin"); await File.WriteAllBytesAsync(source, new byte[400000]);
        var destination = Directory.CreateDirectory(Path.Combine(_directory, "out")).FullName;
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileTransferService().CopyAsync([source], destination, new InlineProgress(_ => cancellation.Cancel()), cancellation.Token));
        Assert.Empty(Directory.GetFiles(destination)); Assert.Equal(400000, new FileInfo(source).Length);
    }
    [Fact] public async Task EmptyFilesCompleteWithFullProgress()
    {
        var source = Path.Combine(_directory, "empty.txt"); await File.WriteAllTextAsync(source, "");
        var destination = Directory.CreateDirectory(Path.Combine(_directory, "out")).FullName; var percent = -1d;
        var result = await new FileTransferService().CopyAsync([source], destination, new InlineProgress(value => percent = value), CancellationToken.None);
        Assert.Single(result); Assert.Equal(100, percent); Assert.Equal(0, new FileInfo(result[0]).Length);
    }
    [Fact] public async Task LargeCopiesBoundProgressDispatchToWholePercentChanges()
    {
        var source = Path.Combine(_directory, "larger.bin");
        using (var stream = File.Create(source)) stream.SetLength(16 * 1024 * 1024);
        var destination = Directory.CreateDirectory(Path.Combine(_directory, "out")).FullName; var reports = new List<double>();
        var result = await new FileTransferService().CopyAsync([source], destination, new InlineProgress(reports.Add), CancellationToken.None);
        Assert.InRange(reports.Count, 2, 102); Assert.Equal(100, reports[^1]); Assert.Equal(new FileInfo(source).Length, new FileInfo(result[0]).Length);
    }
    private sealed class InlineProgress(Action<double> report) : IProgress<double> { public void Report(double value) => report(value); }
    public void Dispose() => Directory.Delete(_directory, true);
}
