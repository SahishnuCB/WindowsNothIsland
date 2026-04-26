using System;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace WindowsNothIsland.Services
{
    public class MediaInfo
    {
        public string Title { get; set; } = "Nothing playing";
        public string Artist { get; set; } = "";
        public string SourceApp { get; set; } = "";
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

            return new MediaInfo
            {
                Title = mediaProperties.Title,
                Artist = mediaProperties.Artist,
                SourceApp = session.SourceAppUserModelId
            };
        }
    }
}