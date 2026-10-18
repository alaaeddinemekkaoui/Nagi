using Pulse.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Pulse.Windows;

public sealed class MediaSessionService : IMediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _artKey;
    private byte[]? _artwork;
    private long _nextArtRead;
    public async Task<MediaSnapshot> SampleAsync(CancellationToken cancellationToken)
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
            var current = _manager.GetCurrentSession();
            GlobalSystemMediaTransportControlsSession? session = null;
            GlobalSystemMediaTransportControlsSessionMediaProperties? properties = null;
            GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback = null;
            var candidates = new List<(GlobalSystemMediaTransportControlsSession Session, GlobalSystemMediaTransportControlsSessionPlaybackInfo Playback)>();
            foreach (var candidate in _manager.GetSessions())
            {
                try { candidates.Add((candidate, candidate.GetPlaybackInfo())); }
                catch (System.Runtime.InteropServices.COMException) { } // A closing player cannot hide another session.
            }
            foreach (var candidate in candidates.OrderByDescending(c => c.Playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .ThenByDescending(c => c.Session == current))
            {
                try
                {
                    var metadata = await candidate.Session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
                    if (!MediaSnapshot.IsDetected(metadata.Title, metadata.Artist, candidate.Playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)) continue;
                    session = candidate.Session; properties = metadata; playback = candidate.Playback; break;
                }
                catch (System.Runtime.InteropServices.COMException) { }
            }
            _session = session;
            if (session is null || properties is null || playback is null) { _artKey = null; _artwork = null; return MediaSnapshot.Empty; }
            var key = session.SourceAppUserModelId + properties.Title + "\0" + properties.Artist;
            if (_artKey != key) { _artKey = key; _artwork = null; _nextArtRead = 0; }
            if (Environment.TickCount64 >= _nextArtRead)
            {
                _nextArtRead = Environment.TickCount64 + (_artwork is null ? 2000 : 15000);
                if (properties.Thumbnail is not null)
                {
                    try
                    {
                        using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(cancellationToken);
                        if (stream.Size is > 0 and < 2_000_000)
                        {
                            using var reader = new DataReader(stream);
                            await reader.LoadAsync((uint)stream.Size).AsTask(cancellationToken);
                            var bytes = new byte[(int)stream.Size]; reader.ReadBytes(bytes);
                            if (_artwork is null || !_artwork.AsSpan().SequenceEqual(bytes)) _artwork = bytes;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* Metadata remains usable without art. */ }
                }
            }
            var controls = playback.Controls;
            return new(string.IsNullOrWhiteSpace(properties.Title) ? "Untitled media" : properties.Title,
                string.IsNullOrWhiteSpace(properties.Artist) ? session.SourceAppUserModelId : properties.Artist,
                playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, true,
                controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled, controls.IsPreviousEnabled, controls.IsNextEnabled, _artwork);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _manager = null; _session = null;
            return MediaSnapshot.Empty with { Artist = "Windows media session unavailable", Error = ex.GetType().Name };
        }
    }
    public async Task<OperationResult> SendAsync(string command)
    {
        try
        {
            var session = _session;
            if (session is null) return OperationResult.Fail("No active media session.");
            var success = command switch
            {
                "previous" => await session.TrySkipPreviousAsync(),
                "next" => await session.TrySkipNextAsync(),
                "toggle" => await session.TryTogglePlayPauseAsync(),
                _ => false
            };
            if (!success && command == "toggle") success = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? await session.TryPauseAsync() : await session.TryPlayAsync();
            return success ? OperationResult.Ok("Media command accepted") : OperationResult.Fail("The media app did not accept this control.");
        }
        catch (Exception ex) { return OperationResult.Fail("Media control unavailable: " + ex.GetType().Name); }
    }
}
