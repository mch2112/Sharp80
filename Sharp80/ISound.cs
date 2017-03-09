using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    internal delegate byte GetSampleCallback();
    internal delegate void SoundEventCallback();

    interface ISound
    {
        void Sample();
        void TrackStep();
        void Dispose();

        bool UseDriveNoise { get; set; }
        bool DriveMotorRunning { set; }
        bool On { get; set; }
        bool Mute { get; set; }
    }   
}
