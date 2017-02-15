using SharpDX;
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
        public bool IsDisposed { get; private set; } = false;
        public void TrackStep() { }

        public SoundNull()
        {
        }
        public void Sample()
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}