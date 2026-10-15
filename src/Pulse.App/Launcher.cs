using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Infrastructure;
using Pulse.Windows;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private FileSearchIndex? _fileIndex;
    private ApplicationCatalog _appCatalog = new([]);
    private QuickSearchWindow? _searchWindow;
    private CancellationTokenSource? _searchCancellation;
    private Task? _searchWork;
    private readonly DispatcherTimer _indexTimer = new() { Interval = TimeSpan.FromMinutes(10) };
    private bool _searchHotkeyAvailable, _searchStarted, _indexBusy;
    private string _searchScope = "";
    private string? _indexError;

    private void InitializeQuickSearch()
    {
        _fileIndex = new(Path.Combine(Path.GetDirectoryName(_settings.FilePath)!, "Search"));
        _windowManager.SearchRequested += ToggleQuickSearch;
        _searchHotkeyAvailable = Report(_windowManager.RegisterSearchHotkey(ViewModel.Settings.SearchHotkey), false);
        _indexTimer.Tick += (_, _) => RebuildSearchIndex();
    }
    private string[] SearchRoots() => ViewModel.Settings.IndexFiles
        ? FileSearchIndex.NormalizeRoots(ViewModel.Settings.SearchFolders.Length > 0 ? ViewModel.Settings.SearchFolders : SearchApplications.StandardFolders()) : [];
    private string SearchStatus()
    {
        var index = _fileIndex!.Snapshot;
        return $"{_appCatalog.Entries.Length} apps · {index.Entries.Length:N0} files & folders"
            + (_indexBusy ? " · Updating…" : !ViewModel.Settings.IndexFiles ? " · File indexing off" : index.Updated is { } updated ? $" · Updated {updated:HH:mm}" : " · Not indexed yet")
            + (index.Limited ? " · 40,000-item limit reached" : "")
            + (index.Unreadable > 0 ? $" · {index.Unreadable} inaccessible items skipped" : "")
            + (_indexError is not null ? " · " + _indexError : _appCatalog.Error is not null ? " · " + _appCatalog.Error : "");
    }
    private void Search_Click(object sender, RoutedEventArgs args) => ToggleQuickSearch();
    private void ToggleQuickSearch()
    {
        if (_closed || _dialogOpen) return;
        if (_searchWindow?.IsOpen == true) { _searchWindow.Hide(); return; }
        ResetPreview();
        _searchWindow ??= new QuickSearchWindow(SearchEntries, SearchStatus, ExecuteSearchAsync, ThemeBrush);
        _searchWindow.Open(ViewModel.Settings);
    }
    private void EnsureSearchScope()
    {
        if (!_searchStarted) return;
        var key = string.Join('\n', SearchRoots());
        if (_searchScope != key) RebuildSearchIndex();
    }
    private void RebuildSearchIndex()
    {
        if (_closed || _fileIndex is null) return;
        var roots = SearchRoots(); var key = string.Join('\n', roots); var scopeChanged = key != _searchScope;
        _searchScope = key; _searchCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token); _searchCancellation = cancellation;
        var previous = _searchWork;
        if (scopeChanged) _fileIndex.Clear(roots);
        _indexBusy = true; _indexError = null; _searchWindow?.IndexChanged();
        _searchWork = Task.Run(async () =>
        {
            try
            {
                if (previous is not null) { try { await previous; } catch (OperationCanceledException) { } }
                cancellation.Token.ThrowIfCancellationRequested();
                await _fileIndex.LoadAsync(roots, cancellation.Token);
                var apps = await SearchApplications.ReadAsync(cancellation.Token);
                if (apps.Diagnostic is not null) _log.Write("search", apps.Diagnostic);
                cancellation.Token.ThrowIfCancellationRequested();
                DispatcherQueue.TryEnqueue(() => { if (!_closed && ReferenceEquals(_searchCancellation, cancellation)) { _appCatalog = apps; _searchWindow?.IndexChanged(); } });
                await _fileIndex.RefreshAsync(roots, cancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.Write("search", "Search index refresh failed", ex);
                DispatcherQueue.TryEnqueue(() => { if (ReferenceEquals(_searchCancellation, cancellation)) _indexError = "Refresh failed; try rebuilding"; });
            }
            finally
            {
                if (!DispatcherQueue.TryEnqueue(() =>
                {
                    if (ReferenceEquals(_searchCancellation, cancellation)) { _searchCancellation = null; _indexBusy = false; if (!_closed) _searchWindow?.IndexChanged(); }
                    cancellation.Dispose();
                })) cancellation.Dispose();
            }
        });
    }
    private SearchEntry[] SearchEntries()
    {
        var entries = new List<SearchEntry>(_appCatalog.Entries);
        var roots = SearchRoots();
        entries.AddRange(_fileIndex!.Snapshot.Entries.Where(e => roots.Any(r => FileSearchIndex.WithinRoot(e.Target, r))));
        foreach (var root in roots) entries.Add(new("root:" + root, Path.GetFileName(root), root, SearchKind.Folder, root));
        foreach (var (id, title, detail) in new[] {
            ("settings", "Open Nagi settings", "Appearance, shortcuts and indexed folders"), ("halo", "Show Nagi", "Open the control center"),
            ("hide", "Hide Nagi", "Keep Nagi running in the tray"), ("work", "Switch to Work", "Lock the Work profile"),
            ("gaming", "Switch to Gaming", "Lock the Gaming profile"), ("focus", "Switch to Focus", "Lock the Focus profile"),
            ("automatic", "Automatic profiles", "Release the manual profile lock"), ("mute", "Toggle output mute", "Mute or unmute Windows output"),
            ("volume-up", "Volume up", "Increase Windows output by 5%"), ("volume-down", "Volume down", "Decrease Windows output by 5%"),
            ("reindex", "Rebuild search index", "Refresh installed apps and selected folders"), ("date", "Copy current date", "Copy today's local date"),
            ("time", "Copy current time", "Copy the current local time") }) entries.Add(new("command:" + id, title, detail, SearchKind.Command, id));
        return entries.ToArray();
    }
    private async Task<OperationResult> ExecuteSearchAsync(SearchEntry entry, string action)
    {
        if (_closed) return OperationResult.Fail("Nagi is closing.");
        if (action == "copy" || entry.Kind == SearchKind.Calculation)
        {
            if (entry.Kind is not (SearchKind.File or SearchKind.Folder or SearchKind.Calculation)) return OperationResult.Fail("This item has no file path.");
            CopySearchText(entry.Target); return OperationResult.Ok("Copied");
        }
        if (action == "reveal")
        {
            if (entry.Kind is not (SearchKind.File or SearchKind.Folder)) return OperationResult.Fail("Choose a file or folder to reveal.");
            var folder = entry.Kind == SearchKind.Folder ? entry.Target : Path.GetDirectoryName(entry.Target) ?? "";
            return await Task.Run(() => ActionLauncher.Launch(new(entry.Title, ActionKind.Folder, folder)));
        }
        if (entry.Kind == SearchKind.Application) return await SearchApplications.OpenAsync(entry, _shutdown.Token);
        if (entry.Kind is SearchKind.File or SearchKind.Folder or SearchKind.Web)
            return await Task.Run(() => ActionLauncher.Launch(new(entry.Title, entry.Kind == SearchKind.Folder ? ActionKind.Folder : entry.Kind == SearchKind.Web ? ActionKind.Url : ActionKind.File, entry.Target)));
        switch (entry.Target)
        {
            case "settings": _searchWindow?.Hide(); Reveal(); Navigate("Settings"); break;
            case "halo": _searchWindow?.Hide(); Navigate("Overview"); Reveal(); break;
            case "hide": HideHalo(); break;
            case "work": case "gaming": case "focus":
                var profile = entry.Target == "work" ? Profile.Work : entry.Target == "gaming" ? Profile.Gaming : Profile.Focus;
                if (!await CommitAsync(s => s with { LockedProfile = profile })) return OperationResult.Fail("Could not save the profile."); break;
            case "automatic": if (!await CommitAsync(s => s with { LockedProfile = null, AutomaticSwitching = true })) return OperationResult.Fail("Could not save profile mode."); break;
            case "mute": return await Task.Run(() => { var audio = _audio.Sample(); return audio.Volume.HasValue ? _audio.SetMute(!audio.Muted) : OperationResult.Fail("Audio output unavailable."); });
            case "volume-up": case "volume-down": return await Task.Run(() => { var audio = _audio.Sample(); return audio.Volume is { } volume ? _audio.SetVolume(volume + (entry.Target == "volume-up" ? 5 : -5)) : OperationResult.Fail("Audio output unavailable."); });
            case "reindex": RebuildSearchIndex(); break;
            case "date": CopySearchText(DateTime.Now.ToString("yyyy-MM-dd")); break;
            case "time": CopySearchText(DateTime.Now.ToString("HH:mm:ss")); break;
            default: return OperationResult.Fail("Unknown search action.");
        }
        return OperationResult.Ok("Done");
    }
    private static void CopySearchText(string value) { var data = new DataPackage(); data.SetText(value); Clipboard.SetContent(data); }

    private void BuildSearchSettings()
    {
        var search = Stack(); search.Children.Add(Heading("Quick search"));
        search.Children.Add(Button("Open quick search", (_, _) => ToggleQuickSearch()));
        search.Children.Add(Button("Search shortcut · " + ViewModels.PulseViewModel.FormatHotkey(ViewModel.Settings.SearchHotkey), async (_, _) => await EditHotkeyAsync(searchShortcut: true)));
        search.Children.Add(Note(_searchHotkeyAvailable ? "Search shortcut registered. Type app:, file:, folder: or > to filter. Try 24 * 7 for the calculator." : "Search shortcut is in use by another app. Choose another shortcut or use Open quick search."));
        search.Children.Add(Toggle("Index local files", "Search filenames in your selected folders. File contents are never read.", ViewModel.Settings.IndexFiles, (s, enabled) => s with { IndexFiles = enabled }));
        foreach (var folder in ViewModel.Settings.SearchFolders.Length > 0 ? ViewModel.Settings.SearchFolders : SearchApplications.StandardFolders()) search.Children.Add(Note(folder));
        search.Children.Add(Button("Add search folder", async (_, _) =>
        {
            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            _dialogOpen = true;
            try
            {
                var folder = await picker.PickSingleFolderAsync(); if (folder is null) return;
                var paths = ViewModel.Settings.SearchFolders.Length > 0 ? ViewModel.Settings.SearchFolders : SearchApplications.StandardFolders();
                if (FileSearchIndex.NormalizeRoots([folder.Path]).Length == 0) { ViewModel.Publish("Choose a local folder.", true); return; }
                await CommitAsync(s => s with { IndexFiles = true, SearchFolders = paths.Append(folder.Path).ToArray() }); BuildSettingsPage();
            }
            catch (Exception ex) { _log.Write("search", "Folder picker failed", ex); ViewModel.Publish("The folder picker could not open.", true); }
            finally { _dialogOpen = false; }
        }));
        search.Children.Add(Button("Edit indexed folders", async (_, _) =>
        {
            var folders = new TextBox { Text = string.Join(Environment.NewLine, ViewModel.Settings.SearchFolders.Length > 0 ? ViewModel.Settings.SearchFolders : SearchApplications.StandardFolders()), AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 150, Header = "One local folder per line · up to 16" };
            var error = Note(""); var content = Stack(); content.Children.Add(folders); content.Children.Add(error);
            var dialog = Dialog("Indexed folders", content);
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var input = folders.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var normalized = FileSearchIndex.NormalizeRoots(input);
                if (input.Length > 16 || input.Length > 0 && normalized.Length != input.Distinct(StringComparer.OrdinalIgnoreCase).Count() || normalized.Any(p => !Directory.Exists(p)))
                { args.Cancel = true; error.Text = "Use up to 16 existing local folders, one per line."; return; }
                var deferral = args.GetDeferral();
                try { if (!await CommitAsync(s => s with { SearchFolders = normalized, IndexFiles = normalized.Length > 0 })) args.Cancel = true; }
                finally { deferral.Complete(); }
            };
            await ShowDialogAsync(dialog); BuildSettingsPage();
        }));
        search.Children.Add(Button("Use standard folders", async (_, _) => { await CommitAsync(s => s with { SearchFolders = [], IndexFiles = true }); BuildSettingsPage(); }));
        search.Children.Add(Button("Rebuild search index", (_, _) => { RebuildSearchIndex(); BuildSettingsPage(); }));
        search.Children.Add(Note(SearchStatus()));
        search.Children.Add(Note("Filename index stays in the Nagi data folder and refreshes every 10 minutes. Up to 40,000 items; system, linked and common build folders are skipped. Web searches open only when selected."));
        SettingsPage.Children.Add(Card(search));
    }
}
