/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    public delegate byte SampleCallback();
    internal delegate void SoundEventCallback();

    public interface ISound
    {
        void Sample();
        void TrackStep();
        void Dispose();

        SampleCallback SampleCallback { set; }
        int SampleRate { get; }
        bool Stopped { get; }
        bool UseDriveNoise { get; set; }
        bool DriveMotorRunning { set; }
        bool On { get; set; }
        bool Mute { get; set; }
    }
}