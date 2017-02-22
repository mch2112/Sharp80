using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal sealed class SectorDescriptor
    {
        public byte TrackNumber { get; set; }
        public byte SectorNumber { get; set; }
        public bool DoubleDensity { get; set; }
        public byte DAM { get; set; }
        public bool SideOne { get; set; }
        public bool CrcError { get; set; }
        public bool InUse { get; set; } = true;
        public ushort SectorSize { get; set; }
        public byte SectorSizeCode { get; set; }
        public byte[] SectorData { get; set; }
        public SectorDescriptor()
        {
        }
        public static SectorDescriptor Empty
        {
            get
            {
                return new SectorDescriptor() { InUse = false };
            }
        }
        public override string ToString()
        {
            return string.Format("Track: {0} Side: {1} Sector: {2} Double Density: {3} Length: {4}{5}", this.TrackNumber, this.SideOne ? "1" : "0", this.SectorNumber, this.DoubleDensity, Lib.ToHexString(SectorSize), this.InUse ? string.Empty : " Unused");
        }
    }
}
