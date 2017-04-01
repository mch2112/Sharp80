/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    public class SoundNull : ISound, IDisposable
    {
        public int SampleRate => 16000;
        public bool Stopped => false;
        public SampleCallback SampleCallback { set; private get; }
        public bool On { get; set; } = false;
        public bool Mute { get; set; } = true;
        public bool UseDriveNoise { get; set; } = false;
        public bool DriveMotorRunning { get; set; } = false;

        public void TrackStep() { }
        public SoundNull() { }
        public void Sample() { }
        public void Dispose() { }
    }
}