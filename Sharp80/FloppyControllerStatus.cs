using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    public class FloppyControllerStatus
    {
        public string OpStatus { get; set; }
        public bool Busy { get; set; }
        public string CommandStatus { get; set; }
        public byte TrackRegister { get; set; }
        public byte SectorRegister { get; set; }
        public byte CommandRegister { get; set; }
        public byte DataRegister { get; set; }
        public bool DoubleDensity { get; set; }
        public bool DRQ { get; set; }
        public bool SeekError { get; set; }
        public bool LostData { get; set; }
        public bool CrcError { get; set; }
        public byte DiskNum { get; set; }
        public byte PhysicalTrackNum { get; set; }
        public string DiskAngle { get; set; }
        public ulong TrackDataIndex { get; set; }
        public byte ByteAtTrackDataIndex { get; set; }
        public bool IndexHole { get; set; }
    }
}
