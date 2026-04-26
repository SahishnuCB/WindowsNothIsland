using System;
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
    }

    public class MediaService
    {
        public async Task<MediaInfo> GetCurrentMediaAsync()
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();

            if (session == null)
                return new MediaInfo();

            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();

            byte[]? thumbnailBytes = null;

            if (mediaProperties.Thumbnail != null)
            {
                using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);

                thumbnailBytes = new byte[stream.Size];
                reader.ReadBytes(thumbnailBytes);
            }

            var duration = timeline.EndTime - timeline.StartTime;

            return new MediaInfo
            {
                Title = mediaProperties.Title,
                Artist = mediaProperties.Artist,
                SourceApp = session.SourceAppUserModelId,
                Thumbnail = thumbnailBytes,
                Position = timeline.Position,
                Duration = duration
            };
        }
    }
}