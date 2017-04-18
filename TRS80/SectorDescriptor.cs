/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;

namespace Sharp80.TRS80
{
    public class SectorDescriptor
    {
        public byte TrackNumber { get; }
        public bool SideOne { get; }
        public byte SectorNumber { get; }
        public bool DoubleDensity { get; }
        public byte DAM { get; }
        public bool CrcError { get; }
        public bool NonIbm { get; } // unused
        public ushort SectorSize => (ushort)SectorData.Length;
        public byte SectorSizeCode => Floppy.GetDataLengthCode(SectorData.Length);
        public byte[] SectorData { get; }
        public bool InUse { get; }
        public byte Side => SideOne ? (byte)1 : (byte)0;

        public SectorDescriptor(byte TrackNumber, byte SectorNumber, bool SideOne, bool DoubleDensity, byte DAM, byte[] Data, bool InUse = true, bool CrcError = false, bool NonIbm = false )
        {
            this.TrackNumber = TrackNumber;
            this.SectorNumber = SectorNumber;
            this.SideOne = SideOne;
            this.DoubleDensity = DoubleDensity;
            this.DAM = DAM;
            this.SectorData = Data;
            this.InUse = InUse;
            this.CrcError = CrcError;
            this.NonIbm = NonIbm;
        }
        
        public static SectorDescriptor Empty { get; } = new SectorDescriptor(0, 0, false, true, Floppy.DAM_NORMAL, new byte[0x100]);

        public override string ToString()
        {
            var inUse = InUse ? "Used" : "Unused";
            return $"Track: {TrackNumber:X2} Side: {Side} Sector: {SectorNumber:X2} Double Density: {DoubleDensity} Length: {SectorSize:X4} {inUse}";
        }
        public bool Deleted => DAM == Floppy.DAM_DELETED;
    }
}
