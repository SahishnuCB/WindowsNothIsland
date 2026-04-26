using NAudio.CoreAudioApi;

namespace WindowsNothIsland.Services
{
    public class AudioService
    {
        private readonly MMDevice _device;

        public AudioService()
        {
            var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        public void ChangeVolume(float delta)
        {
            float newVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar + delta;

            newVolume = Math.Clamp(newVolume, 0f, 1f);

            _device.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
        }

        public float GetVolume()
        {
            return _device.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
    }
}