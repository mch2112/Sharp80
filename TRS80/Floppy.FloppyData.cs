/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharp80.TRS80
{
    public partial class Floppy : IFloppy
    {
        private partial class FloppyData
        {
            public const byte MAX_TRACKS = 80;
            public const int MAX_SECTORS_PER_TRACK = 0x40;

            private const int TRACK_HEADER_LEN = 0x80;
            private const int TRACK_HEADER_SECTORS = TRACK_HEADER_LEN / 2;
            private const int MAX_TRACK_LENGTH_WITH_HEADER = MAX_TRACK_LENGTH + TRACK_HEADER_LEN;
            private const ushort OFFSET_MASK = 0x3FFF;
            private const ushort DOUBLE_DENSITY_MASK = 0x8000;

            private const byte WRITE_PROTECT_BYTE = 0x00;
            private const byte NUM_TRACKS_BYTE = 0x01;
            private const byte TRACK_LEN_LOW_BYTE = 0x02;
            private const byte TRACK_LEN_HIGH_BYTE = 0x03;
            private const byte FLAGS_BYTE = 0x04;

            private const byte WRITE_PROTECT_VAL = 0xFF;
            private const byte NO_WRITE_PROTECT_VAL = 0x00;
            private const byte ZERO_BYTE = 0x00;

            private const byte DISK_HEADER_LENGTH = 0x10;
            private const byte SINGLE_SIDED_FLAG = 0x10;
            private const byte SING_DENS_SING_BYTE_FLAG = 0x40;
            private const byte IGNORE_SING_DENS_FLAG = 0x80;

            private bool changed = false;
            private bool writeProtected = false;

            private bool alwaysSingleByte;
            private bool singleDensitySingleByte;
            private bool ignoreDensity;

            // CONSTRUCTORS

            public FloppyData(byte[] DiskData)
            {
                Deserialize(DiskData);
            }
            public FloppyData(IEnumerable<SectorDescriptor> Sectors, bool WriteProtected) : this(SectorsToDmkBytes(Sectors, WriteProtected))
            {
            }
            public FloppyData(bool Formatted)
            {
                if (Formatted)
                {
                    Deserialize(SectorsToDmkBytes(GetDiskSectorDescriptors(NumTracks: 40,
                                                                           DoubleSided: true,
                                                                           DoubleDensity: true),
                                                  false));
                }
                else
                {
                    Deserialize(UnformattedDmkBytes(40, true));
                    writeProtected = false;
                }
            }

            // TRACKS

            public List<Track> Tracks { get; private set; }
            public Track GetTrack(int TrackNum, bool SideOne)
            {
                return Tracks.FirstOrDefault(t => t.PhysicalTrackNum == TrackNum && t.SideOne == SideOne);
            }
            public SectorDescriptor GetSectorDescriptor(byte TrackNum, bool SideOne, byte SectorIndex)
            {
                return GetTrack(TrackNum, SideOne)?.GetSectorDescriptor(SectorIndex);
            }
            public byte SectorCount(byte TrackNumber, bool SideOne)
            {
                return Tracks.FirstOrDefault(t => t.PhysicalTrackNum == TrackNumber && t.SideOne == SideOne)?.NumSectors ?? 0;
            }

            // PROPERTIES

            public bool WriteProtected
            {
                get => writeProtected;
                set
                {
                    if (writeProtected != value)
                    {
                        changed = true;
                        writeProtected = value;
                    }
                }
            }
            public bool Formatted => Tracks.Any(t => t.Formatted);
            public bool Changed => changed || Tracks.Any(t => t.Changed);
            public byte NumTracks => (byte)(Tracks.Max(t => t.PhysicalTrackNum) + 1);
            public bool DoubleSided => Tracks.Any(t => t.SideOne);

            // SERIALIZATION

            public byte[] Serialize()
            {
                int trackLength = Tracks.Max(t => t.LengthWithHeader);
                byte numTracks = NumTracks;
                int numSides = DoubleSided ? 2 : 1;

                byte[] diskData = new byte[DISK_HEADER_LENGTH + numTracks * numSides * trackLength * 2];

                diskData[WRITE_PROTECT_BYTE] = WriteProtected ? WRITE_PROTECT_VAL : NO_WRITE_PROTECT_VAL;
                diskData[NUM_TRACKS_BYTE] = numTracks;
                ((ushort)trackLength).Split(out diskData[TRACK_LEN_LOW_BYTE], out diskData[TRACK_LEN_HIGH_BYTE]);
                if (numSides == 1)
                    diskData[FLAGS_BYTE] |= SINGLE_SIDED_FLAG;

                int diskCursor = DISK_HEADER_LENGTH;

                byte[] emptyTrack = null;

                for (int i = 0; i < numTracks; i++)
                {
                    for (int j = 0; j < numSides; j++)
                    {
                        var t = Tracks.FirstOrDefault(tt => tt.PhysicalTrackNum == i && tt.SideOne == (j == 1));
                        if (t is null)
                        {
                            emptyTrack = emptyTrack ?? new byte[trackLength];
                            Array.Copy(emptyTrack, 0, diskData, diskCursor, emptyTrack.Length);
                        }
                        else
                        {
                            var d = t.Serialize();
                            Array.Copy(d, 0, diskData, diskCursor, d.Length);
                            for (int k = diskCursor + d.Length; k < diskCursor + trackLength; k++)
                                diskData[k] = (t.DoubleDensity == true) ? FILLER_BYTE_DD : FILLER_BYTE_SD;
                            diskCursor += trackLength;
                        }
                    }
                }
                byte[] ret = new byte[diskCursor];
                Array.Copy(diskData, 0, ret, 0, ret.Length);
                return ret;
            }
            private void Deserialize(byte[] DiskData)
            {
                Tracks = new List<Track>();

                if (DiskData.Length < 0x200)
                    return;

                writeProtected = DiskData[WRITE_PROTECT_BYTE] == WRITE_PROTECT_VAL;
                ushort trackLength = Lib.CombineBytes(DiskData[TRACK_LEN_LOW_BYTE], DiskData[TRACK_LEN_HIGH_BYTE]);
                int numSides = ((DiskData[FLAGS_BYTE] & SINGLE_SIDED_FLAG) == SINGLE_SIDED_FLAG) ? 1 : 2;

                singleDensitySingleByte = (DiskData[FLAGS_BYTE] & SING_DENS_SING_BYTE_FLAG) == SING_DENS_SING_BYTE_FLAG;
                ignoreDensity = (DiskData[FLAGS_BYTE] & IGNORE_SING_DENS_FLAG) == IGNORE_SING_DENS_FLAG;
                alwaysSingleByte = singleDensitySingleByte || ignoreDensity;

                // TODO: Confirm nothing else needed to support ignoreDensity

                int diskCursor = DISK_HEADER_LENGTH;

                byte trackNum = 0;
                while (diskCursor < DiskData.Length)
                {
                    for (int sideNum = 0; sideNum < numSides; sideNum++)
                    {
                        if (DiskData.Length >= diskCursor + trackLength)
                        {
                            Tracks.Add(new Track(trackNum, sideNum == 1, DiskData.Slice(diskCursor, diskCursor + trackLength), singleDensitySingleByte));
                            diskCursor += trackLength;
                        }
                    }
                    trackNum++;
                }
            }

            // HELPERS

            private static byte[] SectorsToDmkBytes(IEnumerable<SectorDescriptor> Sectors, bool WriteProtected)
            {
                byte numTracks = (byte)(Sectors.Max(s => s.TrackNumber) + 1);
                bool doubleSided = Sectors.Any(s => s.SideOne);
                byte numSides = (byte)(doubleSided ? 2 : 1);

                var bytes = new byte[Track.MAX_LENGTH_WITH_HEADER * 80 * 2 + DISK_HEADER_LENGTH];
                bytes[0] = WriteProtected ? WRITE_PROTECT_VAL : NO_WRITE_PROTECT_VAL; // Not write protected
                bytes[1] = numTracks;
                ((ushort)(STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN)).Split(out bytes[2], out bytes[3]);

                byte b = 0;
                if (!doubleSided)
                    b |= SINGLE_SIDED_FLAG;
                bytes[4] = b;

                for (int i = 0x05; i < DISK_HEADER_LENGTH; i++)
                    bytes[i] = ZERO_BYTE;

                int k = DISK_HEADER_LENGTH;
                for (int i = 0; i < numTracks; i++)
                {
                    for (int j = 0; j < numSides; j++)
                    {
                        var trkBytes = Track.ToTrackBytes(Sectors.Where(s => s.TrackNumber == i && (s.SideOne == (j == 1))), Track.DEFAULT_LENGTH_WITH_HEADER);
                        Array.Copy(trkBytes, 0, bytes, k, trkBytes.Length);
                        k += trkBytes.Length;
                    }
                }
                return bytes.Slice(0, k);
            }
            private static byte[] UnformattedDmkBytes(byte NumTracks, bool DoubleSided)
            {
                byte numSides = DoubleSided ? (byte)2 : (byte)1;

                byte[] data = new byte[DISK_HEADER_LENGTH + NumTracks * numSides * (STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN)];

                data[0] = NO_WRITE_PROTECT_VAL; // Not write Protected
                data[1] = NumTracks;
                ((ushort)(STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN)).Split(out data[2], out data[3]);
                data[4] = DoubleSided ? ZERO_BYTE : SINGLE_SIDED_FLAG;    // assumes double density

                return data;
            }
        }
    }
}