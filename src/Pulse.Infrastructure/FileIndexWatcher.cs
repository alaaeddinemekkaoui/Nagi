namespace Pulse.Infrastructure;

// Events only enqueue names. A bounded, debounced worker performs I/O outside the UI thread.
public sealed class FileIndexWatcher : IDisposable
{
    private readonly FileSearchIndex _index;
    private readonly string[] _roots;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly SemaphoreSlim _worker = new(1, 1);
    private readonly CancellationTokenSource _stop;
    private readonly CancellationToken _token;
    private readonly Timer _debounce;
    private bool _rescan, _restart, _disposed;
    public event Action? Changed;
    public event Action<Exception>? Failed;
    public FileIndexWatcher(FileSearchIndex index, string[] roots, CancellationToken token)
    {
        _index = index; _roots = FileSearchIndex.NormalizeRoots(roots);
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _token = _stop.Token;
        _debounce = new(_ => _ = FlushAsync(), null, Timeout.Infinite, Timeout.Infinite);
        lock (_sync) StartWatchers();
    }
    private void StartWatchers()
    {
        foreach (var root in _roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var watcher = new FileSystemWatcher(root) { IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, InternalBufferSize = 16384 };
                watcher.Created += (_, e) => Queue(e.FullPath);
                watcher.Deleted += (_, e) => Queue(e.FullPath);
                watcher.Renamed += (_, e) => { Queue(e.OldFullPath); Queue(e.FullPath); };
                watcher.Error += (_, _) => { lock (_sync) { if (_disposed) return; _rescan = _restart = true; _debounce.Change(500, Timeout.Infinite); } };
                _watchers.Add(watcher); watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { _rescan = true; }
        }
    }
    private void Queue(string path)
    {
        if (!FileSearchIndex.WatchesPath(path, _roots)) return;
        lock (_sync)
        {
            if (_disposed) return;
            if (_pending.Count < 2048) _pending.Add(path); else { _pending.Clear(); _rescan = true; }
            _debounce.Change(500, Timeout.Infinite);
        }
    }
    public void Reconcile()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_rescan || _roots.Any(r => !_watchers.Any(w => w.Path.Equals(r, StringComparison.OrdinalIgnoreCase)) && Directory.Exists(r)))
            { _rescan = _restart = true; _debounce.Change(500, Timeout.Infinite); }
        }
    }
    private async Task FlushAsync()
    {
        try
        {
            await _worker.WaitAsync(_token);
            try
            {
                string[] paths; bool rescan;
                lock (_sync)
                {
                    if (_disposed) return;
                    if (_restart) { foreach (var watcher in _watchers) watcher.Dispose(); _watchers.Clear(); _restart = false; StartWatchers(); }
                    paths = _pending.ToArray(); _pending.Clear(); rescan = _rescan; _rescan = false;
                }
                if (!_index.Snapshot.Roots.SequenceEqual(_roots, StringComparer.OrdinalIgnoreCase)) return;
                if (rescan) await _index.RefreshAsync(_roots, _token);
                else if (paths.Length > 0) await _index.ApplyChangesAsync(_roots, paths, _token);
                if (rescan || paths.Length > 0) Changed?.Invoke();
            }
            finally { _worker.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { lock (_sync) { if (!_disposed) _rescan = true; } Failed?.Invoke(ex); }
    }
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return; _disposed = true;
            _stop.Cancel(); _stop.Dispose(); _debounce.Dispose(); foreach (var watcher in _watchers) watcher.Dispose(); _pending.Clear();
        }
    }
}
