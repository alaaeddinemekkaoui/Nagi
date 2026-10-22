using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Infrastructure;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly List<string> _shelfFiles = [];
    private StackPanel? _transferShelfList;
    private TextBlock? _fileTransferText;
    private Controls.TimerRing? _fileTransferRing;
    private Controls.TimerRing? _utilityUpdateRing;
    private TextBlock? _utilityUpdateText;
    private CancellationTokenSource? _fileTransferCancellation;
    private bool _fileTransferBusy;
    private double _fileTransferProgress;
    private string _fileTransferStatus = "Ready to copy files";
    private string ShelfPath => Path.Combine(Path.GetDirectoryName(_settings.FilePath)!, "transfer-shelf.json");

    private void InitializeTransferCenter()
    {
        try { if (File.Exists(ShelfPath) && new FileInfo(ShelfPath).Length < 65536) _shelfFiles.AddRange((JsonSerializer.Deserialize<string[]>(File.ReadAllText(ShelfPath)) ?? []).Where(path => !string.IsNullOrWhiteSpace(path) && path.Length <= 32767 && Path.IsPathFullyQualified(path)).Distinct(StringComparer.OrdinalIgnoreCase).Take(24)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { _log.Write("transfers", "Could not restore the file shelf", ex); }
        Root.AllowDrop = true;
        Root.DragOver += (_, args) => { if (args.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) args.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy; };
        Root.Drop += async (_, args) =>
        {
            if (!args.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) return;
            var deferral = args.GetDeferral();
            try { AddShelfFiles((await args.DataView.GetStorageItemsAsync()).OfType<global::Windows.Storage.StorageFile>().Select(f => f.Path)); OpenControlCenter("Utilities"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { _log.Write("transfers", "Could not read dropped files", ex); ViewModel.Publish(L.T("Could not add those files."), true); }
            finally { deferral.Complete(); }
        };
    }
    private void AddShelfFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths) if (_shelfFiles.Count < 24 && Path.IsPathFullyQualified(path) && !_shelfFiles.Contains(path, StringComparer.OrdinalIgnoreCase)) _shelfFiles.Add(Path.GetFullPath(path));
        SaveShelf(); RefreshShelf();
    }
    private void SaveShelf()
    {
        try { File.WriteAllText(ShelfPath, JsonSerializer.Serialize(_shelfFiles)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _log.Write("transfers", "Could not save the file shelf", ex); ViewModel.Publish(L.T("The file shelf could not be saved."), true); }
    }
    private void BuildTransferCenter()
    {
        var panel = SettingsCardHeader("Transfer Center", "A file shelf and live download progress, together.", "\uE898");
        AutomationProperties.SetAutomationId(panel, "TransferCenter");
        panel.Children.Add(Note(L.T("Drop files onto Nagi or add them below. The shelf keeps references to up to 24 files. Copying preserves originals and never overwrites an existing file.")));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = Button(L.T("Add files"), async (_, _) =>
        {
            try
            {
                var picker = new global::Windows.Storage.Pickers.FileOpenPicker(); picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                AddShelfFiles((await picker.PickMultipleFilesAsync()).Select(f => f.Path));
            }
            catch (Exception ex) { _log.Write("transfers", "File picker unavailable", ex); ViewModel.Publish(L.T("Could not open file picker."), true); }
        });
        AutomationProperties.SetAutomationId(add, "ShelfAddFiles"); actions.Children.Add(add);
        var copy = Button(L.T("Copy to folder"), async (_, _) => await CopyShelfAsync()); AutomationProperties.SetAutomationId(copy, "ShelfCopyFiles"); actions.Children.Add(copy);
        actions.Children.Add(Button(L.T("Cancel"), (_, _) => _fileTransferCancellation?.Cancel())); panel.Children.Add(actions);
        _fileTransferRing = new Controls.TimerRing { Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Left };
        _fileTransferText = Note(""); panel.Children.Add(_fileTransferRing); panel.Children.Add(_fileTransferText);
        _transferShelfList = Stack(); panel.Children.Add(_transferShelfList);
        _utilityUpdateRing = new Controls.TimerRing { Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Left };
        _utilityUpdateText = Note(""); panel.Children.Add(_utilityUpdateRing); panel.Children.Add(_utilityUpdateText);
        panel.Children.Add(Button(L.T("Clear shelf"), (_, _) => { _shelfFiles.Clear(); SaveShelf(); RefreshShelf(); }));
        panel.Children.Add(Button(L.T("App update downloads"), (_, _) => OpenUtilitySettings("Updates")));
        UtilitiesPage.Children.Add(Card(panel)); RefreshShelf(); RefreshTransferProgress();
    }
    private void RefreshShelf()
    {
        if (_transferShelfList is null) return;
        _transferShelfList.Children.Clear();
        if (_shelfFiles.Count == 0) { _transferShelfList.Children.Add(Note(L.T("Your shelf is empty. Add a few files for later."))); return; }
        foreach (var path in _shelfFiles.ToArray())
        {
            var row = new Grid { ColumnSpacing = 8 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new FontIcon { Glyph = "\uE8A5", FontSize = 17, VerticalAlignment = VerticalAlignment.Center, Foreground = ThemeBrush("PulseAccentBrush") });
            var label = new TextBlock { Text = Path.GetFileName(path), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(label, 1); ToolTipService.SetToolTip(label, path); row.Children.Add(label);
            var remove = Button(L.T("Remove"), (_, _) => { _shelfFiles.Remove(path); SaveShelf(); RefreshShelf(); }); Grid.SetColumn(remove, 2); row.Children.Add(remove);
            _transferShelfList.Children.Add(row);
        }
    }
    private void RefreshTransferProgress()
    {
        if (_fileTransferText is not null) _fileTransferText.Text = L.T(_fileTransferStatus);
        if (_fileTransferRing is not null) { _fileTransferRing.Visibility = _fileTransferBusy || _fileTransferProgress > 0 ? Visibility.Visible : Visibility.Collapsed; _fileTransferRing.Update(_fileTransferProgress / 100, ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush")); }
        if (_utilityUpdateRing is not null) { _utilityUpdateRing.Visibility = _updateTransferBusy || _updateTransferPercent > 0 ? Visibility.Visible : Visibility.Collapsed; _utilityUpdateRing.Update(_updateTransferPercent / 100, ThemeBrush("PulseAccentBrush"), ThemeBrush("MutedBrush")); }
        if (_utilityUpdateText is not null) _utilityUpdateText.Text = _updateTransferBusy ? L.F($"Update download {_updateTransferPercent:0}%") : _updateTransferPercent >= 100 ? L.T("Update verified and ready. Install and restart to finish.") : L.T("No update download in progress.");
        RefreshTransferNotch();
    }
    private async Task CopyShelfAsync()
    {
        if (_fileTransferBusy || _shelfFiles.Count == 0) return;
        try
        {
            var picker = new global::Windows.Storage.Pickers.FolderPicker(); picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync(); if (folder is null || _closed) return;
            _fileTransferBusy = true; _fileTransferProgress = 0; _fileTransferStatus = L.T("Copying files…");
            _fileTransferCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token); RefreshTransferProgress();
            var last = -1;
            var progress = new Progress<double>(value => { if (_closed || !_fileTransferBusy || (int)value == last) return; last = (int)value; _fileTransferProgress = value; _fileTransferStatus = L.F($"Copying files… {value:0}%"); RefreshTransferProgress(); });
            var sources = _shelfFiles.ToArray(); var cancellation = _fileTransferCancellation.Token;
            var copied = await Task.Run(() => new FileTransferService().CopyAsync(sources, folder.Path, progress, cancellation), cancellation);
            _fileTransferProgress = 100; _fileTransferStatus = L.F($"Copied {copied.Length} files"); ViewModel.Publish(_fileTransferStatus, true);
        }
        catch (OperationCanceledException) { _fileTransferStatus = L.T("Copy cancelled. Completed files were kept."); _fileTransferProgress = 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or ArgumentException)
        { _fileTransferStatus = L.T("Copy could not finish. Check the files and destination."); _fileTransferProgress = 0; _log.Write("transfers", "File copy failed", ex); ViewModel.Publish(_fileTransferStatus, true); }
        finally { _fileTransferBusy = false; _fileTransferCancellation?.Dispose(); _fileTransferCancellation = null; if (!_closed) RefreshTransferProgress(); }
    }
}
