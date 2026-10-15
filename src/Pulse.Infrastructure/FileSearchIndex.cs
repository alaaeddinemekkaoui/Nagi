using System.Text.Json;
using Pulse.Core;

namespace Pulse.Infrastructure;

public sealed record FileIndexSnapshot(SearchEntry[] Entries, string[] Roots, DateTimeOffset? Updated, bool Limited = false, int Unreadable = 0);

public sealed class FileSearchIndex(string directory, int maximumEntries = FileSearchIndex.MaximumEntries)
{
    public const int MaximumEntries = 40000;
    private readonly int _maximumEntries = Math.Clamp(maximumEntries, 1, MaximumEntries);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileIndexSnapshot _snapshot = new([], [], null);
    public FileIndexSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public void Clear(string[] roots) => Volatile.Write(ref _snapshot, new([], roots, null));
    private string CachePath => Path.Combine(directory, "file-index.json");
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".svn", "node_modules", "bin", "obj", "AppData", "$RECYCLE.BIN", "System Volume Information" };

    public static string[] NormalizeRoots(IEnumerable<string>? roots) => (roots ?? []).Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p.Trim()).Where(p => Path.IsPathFullyQualified(p) && !p.StartsWith("\\\\", StringComparison.Ordinal) && !p.Contains('\0') && !p.Contains('"'))
        .Select(p => { try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)); } catch { return ""; } })
        .Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToArray();

    public static bool WithinRoot(string path, string root)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root)) return false;
            path = Path.GetFullPath(path); root = Path.GetFullPath(root);
            return path.Equals(root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { return false; }
    }

    public async Task LoadAsync(string[] roots, CancellationToken token)
    {
        roots = NormalizeRoots(roots);
        // A scope change must never continue showing entries from a removed folder.
        if (!Snapshot.Roots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase)) Clear(roots);
        if (Snapshot.Updated is not null) return;
        try
        {
            if (!File.Exists(CachePath) || new FileInfo(CachePath).Length > 24 * 1024 * 1024) return;
            await using var stream = File.OpenRead(CachePath);
            var cached = await JsonSerializer.DeserializeAsync<FileIndexSnapshot>(stream, cancellationToken: token);
            if (cached is null || cached.Roots is null || cached.Entries is null || !cached.Roots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase)) return;
            var entries = cached.Entries.Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Title) && e.Title.Length <= 2048 && e.Detail is not null && e.Kind is SearchKind.File or SearchKind.Folder
                && !string.IsNullOrWhiteSpace(e.Target) && roots.Any(r => WithinRoot(e.Target, r))).Take(_maximumEntries).ToArray();
            Volatile.Write(ref _snapshot, cached with { Entries = entries });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public async Task RefreshAsync(string[] roots, CancellationToken token)
    {
        roots = NormalizeRoots(roots);
        await _gate.WaitAsync(token);
        try
        {
            var snapshot = await Task.Run(() => Scan(roots, _maximumEntries, token), token);
            token.ThrowIfCancellationRequested();
            Volatile.Write(ref _snapshot, snapshot);
            try
            {
                Directory.CreateDirectory(directory);
                var temporary = CachePath + ".tmp";
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot), token);
                File.Move(temporary, CachePath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        finally { _gate.Release(); }
    }
    private static FileIndexSnapshot Scan(string[] roots, int maximumEntries, CancellationToken token)
    {
        var entries = new List<SearchEntry>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Visit selected roots first, before descending into a large project tree.
        var pending = new Queue<string>(roots); var unreadable = 0;
        var options = new EnumerationOptions { IgnoreInaccessible = false, RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System, ReturnSpecialDirectories = false };
        while (pending.TryDequeue(out var folder) && entries.Count < maximumEntries)
        {
            token.ThrowIfCancellationRequested(); if (!seen.Add(folder)) continue;
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(folder, "*", options))
                {
                    token.ThrowIfCancellationRequested(); if (entries.Count >= maximumEntries) break;
                    if (path.Length > 2048) continue;
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0) continue;
                        var isFolder = (attributes & FileAttributes.Directory) != 0;
                        if (isFolder && Skipped.Contains(Path.GetFileName(path))) continue;
                        entries.Add(new(path, Path.GetFileName(path), Path.GetDirectoryName(path) ?? path, isFolder ? SearchKind.Folder : SearchKind.File, path));
                        if (isFolder && !seen.Contains(path)) pending.Enqueue(path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { unreadable++; }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { unreadable++; }
        }
        return new(entries.DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToArray(), roots, DateTimeOffset.Now, entries.Count >= maximumEntries, unreadable);
    }
}
