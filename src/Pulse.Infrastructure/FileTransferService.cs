namespace Pulse.Infrastructure;

public sealed class FileTransferService
{
    public async Task<string[]> CopyAsync(IEnumerable<string> sources, string destination, IProgress<double>? progress, CancellationToken token)
    {
        var files = sources.Distinct(StringComparer.OrdinalIgnoreCase).Take(24).Select(path => new FileInfo(path)).ToArray();
        if (files.Length == 0 || !Directory.Exists(destination)) throw new IOException("Choose files and a destination folder.");
        var total = files.Sum(file => file.Length); long copied = 0; var reported = -1;
        var completed = new List<string>();
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            await using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            string? target = null; FileStream? output = null;
            for (var suffix = 0; suffix < 10000; suffix++)
            {
                target = Path.Combine(destination, suffix == 0 ? file.Name : $"{Path.GetFileNameWithoutExtension(file.Name)} ({suffix}){file.Extension}");
                try { output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true); break; }
                catch (IOException) when (File.Exists(target)) { }
            }
            if (output is null || target is null) throw new IOException("Could not create a unique destination file.");
            try
            {
                await using (output)
                {
                    var buffer = new byte[81920]; int count;
                    while ((count = await input.ReadAsync(buffer, token)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, count), token); copied += count;
                        var percent = total == 0 ? 100 : Math.Clamp(copied * 100d / total, 0, 100);
                        if ((int)percent > reported) { reported = (int)percent; progress?.Report(percent); }
                    }
                    await output.FlushAsync(token);
                }
                completed.Add(target);
            }
            catch { try { File.Delete(target); } catch (IOException) { } catch (UnauthorizedAccessException) { } throw; }
        }
        progress?.Report(100); return completed.ToArray();
    }
}
