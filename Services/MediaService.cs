using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WindowsNothIsland.Services
{
    public class MediaInfo
    {
        public string Title { get; set; } = "Nothing playing";
        public string Artist { get; set; } = "";
        public string SourceApp { get; set; } = "";
        public byte[]? Thumbnail { get; set; }
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public bool IsPlaying { get; set; }
    }

    public class MediaService
    {
        private GlobalSystemMediaTransportControlsSession? _session;

        private async Task<GlobalSystemMediaTransportControlsSession?> GetBestSessionAsync()
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = manager.GetSessions();

            // Prefer actively playing media over paused media
            var playingSession = sessions.FirstOrDefault(session =>
                session.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

            if (playingSession != null)
            {
                _session = playingSession;
                return _session;
            }

            // If nothing is playing, return null so clock mode shows
            _session = null;
            return null;
        }

        public async Task<MediaInfo> GetCurrentMediaAsync()
        {
            var session = await GetBestSessionAsync();

            if (session == null)
                return new MediaInfo();

            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();

            byte[]? thumbnailBytes = null;

            if (mediaProperties.Thumbnail != null)
            {
                using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);

                thumbnailBytes = new byte[stream.Size];
                reader.ReadBytes(thumbnailBytes);
            }

            var position = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? timeline.Position + (DateTimeOffset.Now - timeline.LastUpdatedTime)
                : timeline.Position;

            return new MediaInfo
            {
                Title = mediaProperties.Title,
                Artist = mediaProperties.Artist,
                SourceApp = session.SourceAppUserModelId,
                Thumbnail = thumbnailBytes,
                Position = position,
                Duration = timeline.EndTime - timeline.StartTime,
                IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            };
        }

        public async Task TogglePlayPauseAsync()
        {
            var session = await GetBestSessionAsync();
            if (session == null) return;

            var playback = session.GetPlaybackInfo();

            if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await session.TryPauseAsync();
            else
                await session.TryPlayAsync();
        }

        public async Task NextAsync()
        {
            var session = await GetBestSessionAsync();

            if (session != null)
                await session.TrySkipNextAsync();
        }

        public async Task PreviousAsync()
        {
            var session = await GetBestSessionAsync();

            if (session != null)
                await session.TrySkipPreviousAsync();
        }
    }
}