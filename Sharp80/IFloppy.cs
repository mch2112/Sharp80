using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    internal interface IFloppy
    {
        bool DoubleSided { get; }
        bool Changed { get; }
        byte NumTracks { get; }
        byte SectorCount(byte TrackNum, bool SideOne);
        string FilePath { get; set; }
        bool WriteProtected { get; set; }
        bool Formatted { get; }
        SectorDescriptor GetSectorDescriptor(byte TrackNum, bool SideOne, byte SectorIndex);
    }
}
