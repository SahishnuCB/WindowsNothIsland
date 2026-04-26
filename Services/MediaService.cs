using System;
using System.Collections.Generic;
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
        private List<GlobalSystemMediaTransportControlsSession> _sessions = new();
        private int _selectedIndex = 0;

        private async Task RefreshSessionsAsync()
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            _sessions = manager.GetSessions()
                .Where(s =>
                {
                    var title = s.TryGetMediaPropertiesAsync().GetAwaiter().GetResult().Title;
                    return !string.IsNullOrWhiteSpace(title);
                })
                .ToList();

            if (_sessions.Count == 0)
            {
                _selectedIndex = 0;
                return;
            }

            if (_selectedIndex >= _sessions.Count)
                _selectedIndex = 0;
        }

        private async Task<GlobalSystemMediaTransportControlsSession?> GetBestSessionAsync()
        {
            await RefreshSessionsAsync();

            if (_sessions.Count == 0)
                return null;

            var playingIndex = _sessions.FindIndex(s =>
                s.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

            if (playingIndex != -1 && _selectedIndex == 0)
                _selectedIndex = playingIndex;

            return _sessions[_selectedIndex];
        }

        public async Task NextSessionAsync()
        {
            await RefreshSessionsAsync();

            if (_sessions.Count == 0) return;

            _selectedIndex++;
            if (_selectedIndex >= _sessions.Count)
                _selectedIndex = 0;
        }

        public async Task PreviousSessionAsync()
        {
            await RefreshSessionsAsync();

            if (_sessions.Count == 0) return;

            _selectedIndex--;
            if (_selectedIndex < 0)
                _selectedIndex = _sessions.Count - 1;
        }

        public async Task<MediaInfo> GetCurrentMediaAsync()
        {
            var session = await GetBestSessionAsync();

            if (session == null)
                return new MediaInfo();

            var props = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();

            byte[]? thumbnail = null;

            if (props.Thumbnail != null)
            {
                using var stream = await props.Thumbnail.OpenReadAsync();
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);

                thumbnail = new byte[stream.Size];
                reader.ReadBytes(thumbnail);
            }

            var position = playback.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? timeline.Position + (DateTimeOffset.Now - timeline.LastUpdatedTime)
                : timeline.Position;

            return new MediaInfo
            {
                Title = props.Title,
                Artist = props.Artist,
                SourceApp = session.SourceAppUserModelId,
                Thumbnail = thumbnail,
                Position = position,
                Duration = timeline.EndTime - timeline.StartTime,
                IsPlaying = playback.PlaybackStatus ==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            };
        }

        public async Task TogglePlayPauseAsync()
        {
            var session = await GetBestSessionAsync();
            if (session == null) return;

            var state = session.GetPlaybackInfo().PlaybackStatus;

            if (state == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
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