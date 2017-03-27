/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    public sealed class SectorDescriptor
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
        public static SectorDescriptor Empty => new SectorDescriptor() { InUse = false };
        public override string ToString()
        {
            return string.Format("Track: {0:X2} Side: {1} Sector: {2:X2} Double Density: {3} Length: {4:X4} {5}",
                                 TrackNumber,
                                 SideOne ? "1" : "0",
                                 SectorNumber,
                                 DoubleDensity,
                                 SectorSize.ToHexString(),
                                 InUse ? "Used" : "Unused");
        }
    }
}
