using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.Core;
using Pulse.Infrastructure;
using Pulse.App.Themes;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Pulse.App;

public sealed class LocalMediaWindow : Window
{
    private readonly MediaPlayer _player = new();
    private readonly MediaPlaybackList _playlist = new();
    private readonly List<MediaSource> _sources = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(16, 8, 16, 12) };
    private readonly DiagnosticLog _log;
    private readonly HaloAcrylicBackdrop _glass = new();
    private StorageFile[] _files = [];
    private byte[]? _artwork;
    private string _title = "", _artist = "";
    private int _generation;
    private bool _closed;
    private MediaSnapshot _snapshot = MediaSnapshot.Empty;
    public MediaSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public LocalMediaWindow(DiagnosticLog log)
    {
        _log = log; Title = "Nagi · " + L.T("Local media");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Nagi.ico"));
        var view = new MediaPlayerElement { AreTransportControlsEnabled = true, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        view.SetMediaPlayer(_player);
        AutomationProperties.SetAutomationId(view, "LocalMediaPlayer");
        AutomationProperties.SetAutomationId(_status, "LocalPlaybackStatus");
        var root = new Grid { RequestedTheme = ElementTheme.Dark };
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.Children.Add(view); Grid.SetRow(_status, 1); root.Children.Add(_status); Content = root;
        ApplyAppearance(new PulseSettings());
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(760, 500));
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = presenter.IsMaximizable = true;
        _player.AutoPlay = true;
        _player.MediaOpened += (_, _) => DispatcherQueue.TryEnqueue(UpdateSnapshot);
        _player.PlaybackSession.PlaybackStateChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateSnapshot);
        _playlist.CurrentItemChanged += (_, _) => DispatcherQueue.TryEnqueue(async () => await RefreshMetadataAsync());
        _player.MediaFailed += (_, args) => DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed) return;
            _status.Text = L.T("Windows could not play this file. Check the file or install the required media codec.");
            _log.Write("error", "Local playback failed: " + args.ErrorMessage, args.ExtendedErrorCode);
            Volatile.Write(ref _snapshot, MediaSnapshot.Empty);
        });
        Closed += (_, _) => ReleasePlayback();
    }

    public void ApplyAppearance(PulseSettings settings)
    {
        if (_closed || Content is not Grid root) return;
        root.RequestedTheme = settings.Theme switch { "Light" => ElementTheme.Light, "System" => ElementTheme.Default, _ => ElementTheme.Dark };
        var contrast = new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        var light = root.ActualTheme == ElementTheme.Light;
        _glass.SetLight(light); _glass.SetAkeno(settings.Theme == "Akeno");
        _glass.SetTint(AppAppearance.Tint(settings));
        SystemBackdrop = AppAppearance.UsesGlass(settings) && !contrast && HaloAcrylicBackdrop.IsSupported ? _glass : null;
        root.Background = new SolidColorBrush(contrast
            ? new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background)
            : AppAppearance.Surface(settings, light, SystemBackdrop is not null));
    }

    private void ReleasePlayback()
    {
        if (_closed) return;
        _closed = true; _generation++; Volatile.Write(ref _snapshot, MediaSnapshot.Empty);
        _player.Dispose(); foreach (var source in _sources) source.Dispose(); _sources.Clear();
        _files = []; _artwork = null;
    }

    public void ClosePlayer()
    {
        if (_closed) return;
        ReleasePlayback();
        try { Close(); }
        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80004004))
        { /* WinUI can already be closing secondary windows during application shutdown. */ }
    }

    public void Open(IReadOnlyList<StorageFile> files)
    {
        _generation++; _player.Pause(); _player.Source = null;
        _playlist.Items.Clear(); foreach (var source in _sources) source.Dispose(); _sources.Clear();
        _files = files.ToArray(); _artwork = null;
        foreach (var file in _files)
        {
            var source = MediaSource.CreateFromStorageFile(file); _sources.Add(source);
            _playlist.Items.Add(new MediaPlaybackItem(source));
        }
        _title = _files.FirstOrDefault()?.DisplayName ?? ""; _artist = L.T("Local media");
        _player.Source = _playlist; _player.Play(); Activate();
        _ = RefreshMetadataAsync();
    }

    private async Task RefreshMetadataAsync()
    {
        if (_closed || _files.Length == 0) return;
        var currentIndex = _playlist.CurrentItemIndex;
        var index = currentIndex < _files.Length ? (int)currentIndex : 0;
        var file = _files[index]; var generation = ++_generation;
        _title = file.DisplayName; _artist = L.T("Local media"); _artwork = null;
        UpdateSnapshot();
        try
        {
            var properties = await file.Properties.GetMusicPropertiesAsync();
            if (_closed || generation != _generation) return;
            _title = string.IsNullOrWhiteSpace(properties.Title) ? file.DisplayName : properties.Title;
            _artist = string.IsNullOrWhiteSpace(properties.Artist) ? L.T("Local media") : properties.Artist;
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 256);
            if (_closed || generation != _generation) return;
            if (thumbnail is { Size: > 0 and < 2_000_000 })
            {
                using var reader = new DataReader(thumbnail);
                await reader.LoadAsync((uint)thumbnail.Size);
                if (_closed || generation != _generation) return;
                _artwork = new byte[(int)thumbnail.Size]; reader.ReadBytes(_artwork);
            }
        }
        catch (Exception ex) { if (!_closed) _log.Write("media", "Local metadata unavailable", ex); }
        if (!_closed && generation == _generation) UpdateSnapshot();
    }

    private void UpdateSnapshot()
    {
        if (_closed || _files.Length == 0) return;
        var playing = _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
        var index = _playlist.CurrentItemIndex;
        Volatile.Write(ref _snapshot, new(_title, _artist, playing, true, true, index > 0 && index < _files.Length,
            index + 1 < _files.Length, _artwork));
        _status.Text = L.T(playing ? "Playing" : "Paused") + " · " + _title;
        Title = "Nagi · " + _title;
    }

    public OperationResult Send(string command)
    {
        if (_closed || _files.Length == 0) return OperationResult.Fail("No local media is open.");
        switch (command)
        {
            case "toggle": if (Snapshot.Playing) _player.Pause(); else _player.Play(); break;
            case "previous": _playlist.MovePrevious(); break;
            case "next": _playlist.MoveNext(); break;
            default: return OperationResult.Fail("Unknown media command.");
        }
        return OperationResult.Ok("Local media command accepted.");
    }
}
