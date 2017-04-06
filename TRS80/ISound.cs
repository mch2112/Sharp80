/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    public interface ISound
    {
        void Sample();
        void TrackStep();
        Task Shutdown();

        Func<byte> SampleCallback { set; }
        int SampleRate { get; }
        bool Stopped { get; }
        bool UseDriveNoise { get; set; }
        bool DriveMotorRunning { set; }
        bool On { get; set; }
        bool Mute { get; set; }
    }
}