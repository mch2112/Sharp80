/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    public class SoundNull : ISound
    {
        public int SampleRate => 16000;
        public bool Stopped => false;
        public Func<byte> SampleCallback { set; private get; }
        public bool On { get; set; } = false;
        public bool Mute { get; set; } = true;
        public bool UseDriveNoise { get; set; } = false;
        public bool DriveMotorRunning { get; set; } = false;

        public async Task Shutdown() { await Task.Delay(1); }
        public void TrackStep() { }
        public SoundNull() { }
        public void Sample() { }
    }
}