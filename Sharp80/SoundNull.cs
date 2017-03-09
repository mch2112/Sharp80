/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80
{
    internal class SoundNull : ISound, IDisposable
    {
        public const int SAMPLE_RATE = 16000;

        public bool On { get; set; } = false;
        public bool Mute { get; set; } = true;
        public bool UseDriveNoise { get; set; } = false;
        public bool DriveMotorRunning { get; set; } = false;
        public void TrackStep() { }

        public SoundNull()
        {
        }
        public void Sample()
        {
        }

        public void Dispose()
        {
        }
    }
}