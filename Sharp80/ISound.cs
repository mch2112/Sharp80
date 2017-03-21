/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal delegate byte GetSampleCallback();
    internal delegate void SoundEventCallback();

    interface ISound
    {
        void Sample();
        void TrackStep();
        void Dispose();

        bool Stopped { get; }
        bool UseDriveNoise { get; set; }
        bool DriveMotorRunning { set; }
        bool On { get; set; }
        bool Mute { get; set; }
    }   
}
