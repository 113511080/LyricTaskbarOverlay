using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace LyricTaskbarOverlay;

public sealed class WindowsMediaWatcher
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to initialize GSMTC: " + ex.Message);
        }
    }

    public async Task<SpotifyPlayerState?> GetCurrentMediaStateAsync()
    {
        if (_sessionManager == null) return null;

        try
        {
            var session = _sessionManager.GetCurrentSession();
            if (session == null) return null;

            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();
            
            var info = await session.TryGetMediaPropertiesAsync();

            if (info == null || string.IsNullOrWhiteSpace(info.Artist) || string.IsNullOrWhiteSpace(info.Title))
                return null;

            var track = new TrackInfo(info.Artist, info.Title);
            bool isPlaying = playback != null && playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            TimeSpan progress = timeline != null ? timeline.Position : TimeSpan.Zero;
            DateTimeOffset timestamp = timeline != null ? timeline.LastUpdatedTime : DateTimeOffset.Now;

            return new SpotifyPlayerState(track, progress, isPlaying, timestamp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to get media state: " + ex.Message);
            return null;
        }
    }
}
