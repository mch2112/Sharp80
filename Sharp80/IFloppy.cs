using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    public interface IFloppy
    {
        bool DoubleSided { get; }
        bool Changed { get; }
        byte NumTracks { get; }
        byte SectorCount(byte TrackNum, bool SideOne);
        string FilePath { get; set; }
        string FileDisplayName { get; }
        bool WriteProtected { get; set; }
        bool Formatted { get; }
        SectorDescriptor GetSectorDescriptor(byte TrackNum, bool SideOne, byte SectorIndex);
    }
}
