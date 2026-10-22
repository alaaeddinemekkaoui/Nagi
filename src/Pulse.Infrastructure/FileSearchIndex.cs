using System.Text.Json;
using Pulse.Core;

namespace Pulse.Infrastructure;

public sealed record FileIndexSnapshot(SearchEntry[] Entries, string[] Roots, DateTimeOffset? Updated, bool Limited = false, int Unreadable = 0);

public sealed partial class FileSearchIndex(string directory, int maximumEntries = FileSearchIndex.MaximumEntries)
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
    public static bool WatchesPath(string path, string[] roots)
    {
        if (path.Length > 2048) return false;
        var root = roots.FirstOrDefault(r => WithinRoot(path, r));
        if (root is null) return false;
        for (var current = path; current is not null && !current.Equals(root, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
            if (Skipped.Contains(Path.GetFileName(current))) return false;
        return true;
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
    public async Task ApplyChangesAsync(string[] roots, IEnumerable<string> changes, CancellationToken token)
    {
        roots = NormalizeRoots(roots);
        await _gate.WaitAsync(token);
        try
        {
            var before = Snapshot;
            if (!before.Roots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase)) return;
            var entries = before.Entries.ToDictionary(e => e.Target, StringComparer.OrdinalIgnoreCase);
            var dirty = changes.Where(p => roots.Any(r => WithinRoot(p, r)))
                .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Remove affected entries in one pass, rather than scanning all 40,000 entries per event.
            foreach (var key in entries.Keys.Where(p => HasChangedAncestor(p, dirty)).ToArray()) entries.Remove(key);
            foreach (var path in dirty.Where(p => !HasChangedAncestor(Path.GetDirectoryName(p), dirty)))
            {
                token.ThrowIfCancellationRequested();
                if (!Eligible(path, roots)) continue;
                try
                {
                    var folder = Directory.Exists(path);
                    if (!folder && !File.Exists(path)) continue;
                    if (entries.Count < _maximumEntries && !roots.Contains(path, StringComparer.OrdinalIgnoreCase)) entries[path] = Entry(path, folder);
                    if (folder)
                        foreach (var entry in Scan([path], Math.Max(1, _maximumEntries - entries.Count), token).Entries)
                            if (entries.Count < _maximumEntries) entries[entry.Target] = entry;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            var next = new FileIndexSnapshot(entries.Values.ToArray(), roots, DateTimeOffset.Now, entries.Count >= _maximumEntries, before.Unreadable);
            token.ThrowIfCancellationRequested();
            // A UI scope change can clear the snapshot while this worker is running.
            if (!Snapshot.Roots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase)) return;
            Volatile.Write(ref _snapshot, next);
            await SaveSnapshotAsync(next, token);
        }
        finally { _gate.Release(); }
    }
    private static bool HasChangedAncestor(string? path, HashSet<string> dirty)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if (dirty.Contains(current)) return true;
        return false;
    }
    private async Task SaveSnapshotAsync(FileIndexSnapshot snapshot, CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(CachePath + ".tmp", JsonSerializer.Serialize(snapshot), token);
            File.Move(CachePath + ".tmp", CachePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    private static SearchEntry Entry(string path, bool folder) => new(path, Path.GetFileName(path), Path.GetDirectoryName(path) ?? path, folder ? SearchKind.Folder : SearchKind.File, path);
    private static bool Eligible(string path, string[] roots)
    {
        if (path.Length > 2048) return false;
        var root = roots.FirstOrDefault(r => WithinRoot(path, r));
        if (root is null) return false;
        try
        {
            for (var current = path; current is not null && WithinRoot(current, root); current = Path.GetDirectoryName(current))
            {
                if (Directory.Exists(current) && Skipped.Contains(Path.GetFileName(current))) return false;
                if ((File.GetAttributes(current) & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0) return false;
                if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
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
