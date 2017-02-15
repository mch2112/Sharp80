using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal sealed partial class DMK : Floppy
    {
        private bool alwaysSingleByte;
        private bool singleDensitySingleByte;
        private bool ignoreDensity;

        private const int TRACK_HEADER_LEN = 0x80;
        private const int TRACK_HEADER_SECTORS = TRACK_HEADER_LEN / 2;
        private const int MAX_TRACK_LENGTH_WITH_HEADER = MAX_TRACK_LENGTH + TRACK_HEADER_LEN;
        private const ushort OFFSET_MASK = 0x3FFF;
        private const ushort DOUBLE_DENSITY_MASK = 0x8000;
        private const byte WRITE_PROTECT_BYTE = 0x00;
        private const byte NO_WRITE_PROTECT_BYTE = 0x00; 
        private const byte NUM_TRACKS_BYTE = 0x01;
        private const byte TRACK_LEN_LOW_BYTE = 0x02;
        private const byte TRACK_LEN_HIGH_BYTE = 0x03;
        private const byte FLAGS_BYTE = 0x04;
        private const byte ZERO_BYTE = 0x00;
        private const byte DISK_HEADER_LENGTH = 0x10;
        private const byte SINGLE_SIDED_FLAG = 0x10;
        private const byte SING_DENS_SING_BYTE_FLAG = 0x40;
        private const byte IGNORE_SING_DENS_FLAG = 0x80;
        private const byte WRITE_PROTECT_VAL = 0xFF;
        
        public DMK() : base()
        {
        }

        public DMK(byte[] DiskData)
        {
            Deserialize(DiskData);
        }
      
        public static Floppy MakeFloppyFromFile(byte[] b, string filename)
        {
            var fn = Floppy.ConvertWindowsFilePathToTRSDOSFileName(filename);

            byte neededSectors  = (byte)Math.Ceiling((double)b.Length / (double)0x100);
            byte neededGranules = (byte)Math.Ceiling((double)neededSectors / (double)0x003);
            byte neededTracks   = (byte)Math.Ceiling((double)neededGranules / (double)0x006);
            
            // for now only using tracks after the directory track, so about 85K max.
            // could make more room by using tracks 0 to 16.

            if (neededTracks > 21) // file too big
                return null; // TODO: Error message

            var sectors = GetDiskSectorDescriptors(NumTracks: 40, DoubleSided: false, DoubleDensity: true);
            
            byte trackNum = 18;
            byte sectorNum = 0;
            int cmdFileCursor = 0;
            int lastUsedSectorNum = 0;

            var sectorsToUse = sectors.Where(s => s.TrackNumber >= 18 && s.SideOne == false)
                                      .OrderBy(s => s.SectorNumber)
                                      .OrderBy(s => s.TrackNumber)
                                      .GetEnumerator();

            for (int i = 0; i < neededSectors; i++)
            {
                sectorsToUse.MoveNext();

                Array.Copy(b, cmdFileCursor, sectorsToUse.Current.SectorData, 0, Math.Min(0x100, b.Length - cmdFileCursor));
                cmdFileCursor += 0x100;

                lastUsedSectorNum = sectorNum;
            }

            // Update GAT
            var gatSector = sectors.First(s => s.TrackNumber == 17 && s.SectorNumber == 1);

            byte granulesToAllocate = neededGranules;
            byte byteNum = 17;
            while (granulesToAllocate > 0)
            {
                byte granulesThisRecord = Math.Min(granulesToAllocate, (byte)0x06);
                gatSector.SectorData[byteNum++] = (byte)((0x01 << granulesThisRecord) - 1);
                granulesToAllocate -= granulesThisRecord;
            }

            // Update HIT
            var hitSector = sectors.First(s => s.TrackNumber == 17 && s.SectorNumber == 2);
            hitSector.SectorData[0] = HashFilename(fn);
            // Set directory entry
            var dirSector = sectors.First(s => s.TrackNumber == 17 && s.SectorNumber == 3);

            dirSector.SectorData[0] = 0x10; // normal visibility and protection
            dirSector.SectorData[1] = 12;   // Dec 1999
            dirSector.SectorData[2] = 99;
            dirSector.SectorData[3] = (byte)((b.Length) & 0xFF); // EOF byte location in last sector
            dirSector.SectorData[4] = 0x00; // Logical record length = 0x100

            for (int i = 0; i < 11; i++)
                dirSector.SectorData[i + 5] = (byte)fn[i];

            // Passwords = None
            dirSector.SectorData[16] = 0xEF;
            dirSector.SectorData[17] = 0x5C;
            dirSector.SectorData[18] = 0xEF;
            dirSector.SectorData[19] = 0x5C;

            // File length
            Lib.SplitBytes((ushort)(neededSectors), out dirSector.SectorData[20], out dirSector.SectorData[21]);

            trackNum = 18;
            byteNum = 22;
            int remainingNeededGranules = neededGranules;

            for (int i = 0; i < 26; i++)
            {
                if (remainingNeededGranules > 0)
                {
                    // store a max of 18 granules per extent
                    byte granulesThisExtent = (byte)Math.Min(remainingNeededGranules, 18);

                    dirSector.SectorData[byteNum++] = trackNum;
                    dirSector.SectorData[byteNum++] = granulesThisExtent;

                    trackNum += 3;
                    remainingNeededGranules -= granulesThisExtent;
                }
                else
                {
                    dirSector.SectorData[byteNum++] = 0xFF;
                    dirSector.SectorData[byteNum++] = 0xFF;
                    break;
                }
            }
            return MakeFloppy(Sectors: sectors,
                                WriteProtected: false,
                                OriginalFileType: FileType.DMK);
        }
        public static Floppy MakeBlankFloppy(byte NumTracks, bool DoubleSided, bool Formatted)
        {
            return Formatted ? MakeFloppy(Sectors: GetDiskSectorDescriptors(NumTracks: NumTracks,
                                                                            DoubleSided: DoubleSided,
                                                                            DoubleDensity: true),
                                          WriteProtected: false,
                                          OriginalFileType: FileType.DMK)
                             : MakeUnformattedFloppy(NumTracks: NumTracks,
                                                     DoubleSided: DoubleSided);
        }
        public override bool IsDoubleDensity(byte TrackNumber, bool SideOne, byte SectorNumber)
        {
            return SafeGetTrack(TrackNumber, SideOne).IsDoubleDensity(SectorNumber);
        }
        public override byte[] GetTrackData(byte TrackNumber, bool SideOne)
        {
            return SafeGetTrack(TrackNumber, SideOne).Data;
        }
        public override byte[] GetSectorData(byte TrackNumber, bool SideOne, byte SectorNumber)
        {
            return SafeGetTrack(TrackNumber, SideOne).GetSectorData(SectorNumber);
        }
        public override byte GetDAM(byte TrackNumber, bool SideOne, byte SectorNumber)
        {
            return SafeGetTrack(TrackNumber, SideOne).GetDAM(SectorNumber);
        }

        public override void WriteTrackData(byte TrackNumber, bool SideOne, byte[] Data, bool[] DoubleDensity)
        {
            if (!writeProtected)
            {
                SafeGetTrack(TrackNumber, SideOne).Deserialize(Data, DoubleDensity);
                UpdateDoubleDensity();
                Changed = true;
            }
        }

        public override byte[] Serialize(bool ForceDMK)
        {
            if (ForceDMK)
                return SerializeToDMK();
            else
                switch (OriginalFileType)
                {
                    case FileType.JV1:
                        return SerializeToJV1();
                    case FileType.JV3:
                        return SerializeToJV3();
                    default:
                        return SerializeToDMK();
                }
        }
        public override void Deserialize(System.IO.BinaryReader Reader)
        {
            int dataLength = Reader.ReadInt32();
            Deserialize(Reader.ReadBytes(dataLength));
            FilePath = Reader.ReadString();
        }
        
        private void UpdateDoubleDensity()
        {
            if (tracksSide0.Concat(tracksSide1).Any(t => !t.DoubleDensity.HasValue))
                DoubleDensity = null;
            else if (tracksSide0.Concat(tracksSide1).All(t => t.DoubleDensity.Value))
                DoubleDensity = true;
            else if (tracksSide0.Concat(tracksSide1).All(t => !t.DoubleDensity.Value))
                DoubleDensity = false;
            else
                DoubleDensity = null;
        }
        
        private static List<SectorDescriptor> GetDiskSectorDescriptors(byte NumTracks, bool DoubleSided, bool DoubleDensity)
        {
            const int SECTOR_SIZE = 0x100;

            var sectors = new List<SectorDescriptor>();
            int numSides = DoubleSided ? 2 : 1;
            byte sectorsPerTrack = DoubleDensity ? (byte)18 : (byte)10;
            byte startingSectorNumber = DoubleDensity ? (byte)1 : (byte)0;

            const byte DIRECTORY_TRACK = 17;

            for (byte i = 0; i < NumTracks; i++)
                for (int j = 0; j < numSides; j++)
                {
                    byte secNum = startingSectorNumber;
                    for (byte k = 0; k < sectorsPerTrack; k++)
                    {
                        var sd = new SectorDescriptor(i, secNum)
                        {
                            SideOne = j > 0,
                            InUse = true,
                            SectorSize = SECTOR_SIZE,
                            SectorSizeCode = GetDataLengthCode(SECTOR_SIZE),
                            SectorData = new byte[SECTOR_SIZE],
                            DAM = DAM_NORMAL,
                            DoubleDensity = DoubleDensity,
                            NonIbm = false,
                            CrcError = false
                        };

                        if (i != DIRECTORY_TRACK)
                            for (int l = 0; l < SECTOR_SIZE; l++)
                                sd.SectorData[l] = 0xE5;

                        sectors.Add(sd);

                        if (DoubleDensity)
                        {
                            // Interleave sectors by three
                            if (secNum < 16)
                                secNum += 3;
                            else
                                secNum -= 14;
                        }
                        else
                        {
                            if (secNum < 5)
                                secNum += 5;
                            else
                                secNum -= 4;
                        }
                    }
                }

            var gatSector = sectors.First(s => s.TrackNumber == 17 && !s.SideOne && s.SectorNumber == startingSectorNumber);
            gatSector.SectorData[0x00] = 0x01; // GAT 
            gatSector.SectorData[0x11] = 0x3F; // GAT 
            
            gatSector.SectorData[0xCE] = 0xD3; // "PASSWORD"
            gatSector.SectorData[0xCF] = 0x8F;

            gatSector.SectorData[0xD0] = (byte)'T';
            gatSector.SectorData[0xD1] = (byte)'R';
            gatSector.SectorData[0xD2] = (byte)'S';
            gatSector.SectorData[0xD3] = (byte)'D';
            gatSector.SectorData[0xD4] = (byte)'O';
            gatSector.SectorData[0xD5] = (byte)'S';
            gatSector.SectorData[0xD6] = (byte)' ';
            gatSector.SectorData[0xD7] = (byte)' ';
            gatSector.SectorData[0xD8] = (byte)'0';
            gatSector.SectorData[0xD9] = (byte)'1';
            gatSector.SectorData[0xDA] = (byte)'/';
            gatSector.SectorData[0xDB] = (byte)'0';
            gatSector.SectorData[0xDC] = (byte)'1';
            gatSector.SectorData[0xDD] = (byte)'/';
            gatSector.SectorData[0xDE] = (byte)'8';
            gatSector.SectorData[0xDF] = (byte)'0';

            gatSector.SectorData[0xE0] = 0x0D; // no "Auto" command set

            var hitSector = sectors.First(s => s.TrackNumber == 17 && !s.SideOne && s.SectorNumber == startingSectorNumber + 1);
            for (int i = 0xE0; i < 0x100; i++)
                hitSector.SectorData[i] = 0xFF;

            return sectors;

        }
        private static DMK MakeFloppy(List<SectorDescriptor> Sectors, bool WriteProtected, FileType OriginalFileType)
        {
            byte numTracks = (byte)(Sectors.Max(s => s.TrackNumber) + 1);
            bool doubleSided = Sectors.Exists(s => s.SideOne);

            var tracks = Sectors.OrderBy(s => s.SideOne)
                                .OrderBy(s => s.TrackNumber)
                                .GroupBy(s => new { TrackNumber = s.TrackNumber, SideOne = s.SideOne });

            var bytes = new List<byte>((STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN) * tracks.Count())
            {
                WriteProtected ? WRITE_PROTECT_BYTE : ZERO_BYTE, // Not write protected
                numTracks
            };
            Lib.SplitBytes(STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN, out byte low, out byte high);
            bytes.Add(low);
            bytes.Add(high);

            byte b = SING_DENS_SING_BYTE_FLAG;
            if (!doubleSided)
                b |= SINGLE_SIDED_FLAG;
            bytes.Add(b);

            for (int i = 0x05; i < DISK_HEADER_LENGTH; i++)
                bytes.Add(ZERO_BYTE);

            ushort crc;

            foreach (var track in tracks)
            {
                int trackStartIndex = bytes.Count;
                int numSectors = track.Count();
                var sectors = track.GetEnumerator();
                for (byte j = 0; j < TRACK_HEADER_SECTORS; j++)
                {
                    if (sectors.MoveNext())
                    {
                        Lib.SplitBytes(sectors.Current.DoubleDensity ? DOUBLE_DENSITY_MASK : (ushort)0x0001, out low, out high);
                        bytes.Add(low);
                        bytes.Add(high);
                    }
                    else
                    {
                        bytes.Add(ZERO_BYTE);
                        bytes.Add(ZERO_BYTE);
                    }
                }

                int fillerBytes = track.First().DoubleDensity ? 62 : 32;
                for (int k = 0; k < fillerBytes; k++)
                    bytes.Add(FILLER_BYTE);

                foreach (var sector in track)
                {
                    int sectorStartIndex = bytes.Count;

                    fillerBytes = track.First().DoubleDensity ? 34 : 17;
                    for (int k = 0; k < fillerBytes; k++)
                        bytes.Add(ZERO_BYTE);

                    if (sector.DoubleDensity)
                    {
                        bytes.Add(0xA1);
                        bytes.Add(0xA1);
                        bytes.Add(0xA1);
                        bytes.Add(IDAM);
                        crc = CRC_RESET_A1_A1_A1_FE;
                    }
                    else
                    {
                        bytes.Add(IDAM);
                        crc = CRC_RESET_FE;
                    }

                    byte[] nextBytes = new byte[] { sector.TrackNumber, sector.SideOne ? (byte)0x01 : ZERO_BYTE, sector.SectorNumber, sector.SectorSizeCode };

                    foreach (byte bb in nextBytes)
                    {
                        bytes.Add(bb);
                        crc = Lib.Crc(crc, bb);
                    }

                    Lib.SplitBytes(crc, out low, out high);
                    bytes.Add(high);
                    bytes.Add(low);

                    // filler
                    fillerBytes = sector.DoubleDensity ? 34 : 12;
                    for (int k = 0; k < fillerBytes; k++)
                        bytes.Add(0x00);

                    crc = sector.DoubleDensity ? CRC_RESET_A1_A1_A1 : CRC_RESET;

                    // DAM
                    bytes.Add(sector.DAM);
                    crc = Lib.Crc(crc, sector.DAM);

                    for (int k = 0; k < sector.SectorSize; k++)
                    {
                        bytes.Add(sector.SectorData[k]);
                        crc = Lib.Crc(crc, sector.SectorData[k]);
                    }
                    if (sector.CrcError)
                        crc = (ushort)~crc;
                    Lib.SplitBytes(crc, out low, out high);
                    bytes.Add(high);
                    bytes.Add(low);
                }
                while (bytes.Count < trackStartIndex + STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN)
                    bytes.Add(FILLER_BYTE);
            }

            var f = new DMK(bytes.ToArray())
            {
                OriginalFileType = OriginalFileType
            };
            return f;
        }
        private static DMK MakeUnformattedFloppy(byte NumTracks, bool DoubleSided)
        {
            byte numSides = DoubleSided ? (byte)2 : (byte)1;

            byte[] data = new byte[DISK_HEADER_LENGTH + NumTracks * numSides * (STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN)];

            data[0] = NO_WRITE_PROTECT_BYTE; // Not write Protected
            data[1] = NumTracks;
            Lib.SplitBytes((STANDARD_TRACK_LENGTH_DOUBLE_DENSITY + TRACK_HEADER_LEN), out data[2], out data[3]);
            data[4] = DoubleSided ? ZERO_BYTE : SINGLE_SIDED_FLAG;    // assumes double density

            return new DMK(data);
        }
        
        private byte[] GetTrackHeader(Track Track)
        {
            byte[] header = new byte[TRACK_HEADER_LEN];
            bool[] dd = Track.DoubleDensityArray;
            ushort[] il = GetIdamLocations(Track.Data, dd, 0, Track.Data.Length);
            byte sectorCount = (byte)Math.Min(dd.Length, il.Length);
            for (byte i = 0; i < sectorCount; i++)
            {
                ushort headerVal = (ushort)(il[i] + TRACK_HEADER_LEN);
                if (dd[i])
                    headerVal |= DOUBLE_DENSITY_MASK;

                Lib.SplitBytes(headerVal, out byte low, out byte high);
                header[i * 2] = low;
                header[i * 2 + 1] = high;
            }
            return header;
        }
        private ushort[] GetIdamLocations(byte[] DiskData, bool[] DoubleDensity, int Start, int Length)
        {
            var locations = new List<ushort>();
            int doubleDensityIndex = 0;
            int end = Start + Length;
            for (int i = Start; i < end && doubleDensityIndex < DoubleDensity.Length; i++)
            {
                if (DiskData[i] == IDAM)
                {
                    locations.Add((ushort)(i - Start));

                    int skipFactor = DoubleDensity[doubleDensityIndex] ? 1 : 2;

                    i += 4 * skipFactor;
                    ushort sectorLength = GetDataLengthFromCode(DiskData[i++]);

                    // skip crc
                    i += 2 * skipFactor;

                    // skip data
                    while (i < end && !IsDAM(DiskData[i]))
                        i += skipFactor;

                    i += sectorLength * skipFactor;

                    doubleDensityIndex++;
                }
            }
            return locations.ToArray();
        }
        private byte[] UndoubleTrack(byte[] DiskData, int diskCursor, ushort TrackLength, ushort[] IdamLocations, bool[] doubleDensity)
        {
            byte[] trackData;
            bool[] nukeMask = new bool[TrackLength];
            int nukeCount = 0;
            int marker;
            int nextMarker = IdamLocations.Length > 0 ? IdamLocations[0] : TrackLength;

            const int LOOK_AHEAD = 0x10;
            bool singleDensity = false;

            for (int i = 0; i < IdamLocations.Length; i++)
            {
                singleDensity = !doubleDensity[i];
                marker = nextMarker;
                if ((i == IdamLocations.Length - 1) || (IdamLocations[i + 1] == 0))
                    nextMarker = TrackLength;
                else
                {
                    nextMarker = IdamLocations[i + 1];
                }

                if (singleDensity)
                    for (int j = marker - LOOK_AHEAD + 1; j < nextMarker - LOOK_AHEAD; j += 2)
                    {
                        if (DiskData[diskCursor + TRACK_HEADER_LEN + j] == DiskData[diskCursor + TRACK_HEADER_LEN + j - 1])
                        {
                            nukeMask[j] = true;
                            nukeCount++;
                        }
                    }
            }

            trackData = new byte[TrackLength - nukeCount];
            ushort m = 0;
            nukeCount = 0;
            int k = 0;
            for (int i = 0; i < TrackLength; i++)
            {
                if (k < IdamLocations.Length && IdamLocations[k] == i)
                    IdamLocations[k++] = m;

                if (nukeMask[i])
                    nukeCount++;
                else
                    trackData[m++] = DiskData[diskCursor + TRACK_HEADER_LEN + i];
            }
            for (int i = 0; i < IdamLocations.Length; i++)
                if (IdamLocations[i] > 0 && trackData[IdamLocations[i]] != Floppy.IDAM)
                    throw new Exception("IDAM Not found: undouble");
            return trackData;
        }
        private bool IsDAM(byte Byte)
        {
            return (Byte >= 0xF8) && (Byte <= 0xFB);
        }

        private byte[] SerializeToDMK()
        {
            int trackLength = tracksSide0.Concat(tracksSide1).Max(t => t.Data.Length);

            int numTracks;
            
            if (numSides == 2)
                numTracks = Math.Max(tracksSide0.Count, tracksSide1.Count);
            else
                numTracks = tracksSide0.Count;

            byte[] diskData = new byte[DISK_HEADER_LENGTH + numTracks * numSides * (trackLength + TRACK_HEADER_LEN)];

            diskData[WRITE_PROTECT_BYTE] = writeProtected ? WRITE_PROTECT_VAL : NO_WRITE_PROTECT_BYTE;
            diskData[NUM_TRACKS_BYTE] = this.numTracks;
            Lib.SplitBytes((ushort)(trackLength + TRACK_HEADER_LEN), out diskData[TRACK_LEN_LOW_BYTE], out diskData[TRACK_LEN_HIGH_BYTE]);
            if (numSides == 1)
                diskData[FLAGS_BYTE] |= SINGLE_SIDED_FLAG;

            if (true /*this.singleDensitySingleByte */)
                diskData[FLAGS_BYTE] |= SING_DENS_SING_BYTE_FLAG;
            if (this.ignoreDensity)
                diskData[FLAGS_BYTE] |= IGNORE_SING_DENS_FLAG;

            int diskCursor = DISK_HEADER_LENGTH;

            byte[] emptyTrack = null;

            for (int i = 0; i < this.numTracks; i++)
            {
                for (int j = 0; j < this.numSides; j++)
                {
                    Track t;
                    List<Track> tracks = (j == 0) ? this.tracksSide0 : this.tracksSide1;
                    if (i < tracks.Count)
                    {
                        t = tracks[i];
                        byte[] header = GetTrackHeader(t);
                        Array.Copy(header, 0, diskData, diskCursor, TRACK_HEADER_LEN);
                        Array.Copy(t.Data, 0, diskData, diskCursor + TRACK_HEADER_LEN, Math.Min(t.Data.Length, diskData.Length - diskCursor));
                        for (int k = diskCursor + TRACK_HEADER_LEN + t.Data.Length; k < diskCursor + TRACK_HEADER_LEN + trackLength; k++)
                            diskData[k] = FILLER_BYTE;
                        diskCursor += trackLength + TRACK_HEADER_LEN;
                    }
                    else
                    {
                        emptyTrack = emptyTrack ?? new byte[trackLength];
                        Array.Copy(emptyTrack, 0, diskData, diskCursor, emptyTrack.Length);
                    }
                }
            }
            byte[] ret = new byte[diskCursor];
            Array.Copy(diskData, 0, ret, 0, ret.Length);
            return ret;
        }
        private void Deserialize(byte[] DiskData)
        {
            try
            {
                Reset();

                if (DiskData.Length < 0x200)
                {
                    Log.LogMessage(string.Format("Invalid DMK format: Too short ({0} bytes)", DiskData.Length));
                    return;
                }

                writeProtected = DiskData[WRITE_PROTECT_BYTE] == WRITE_PROTECT_VAL;
                numTracks = DiskData[NUM_TRACKS_BYTE];
                ushort trackLength = (ushort)(Lib.CombineBytes(DiskData[TRACK_LEN_LOW_BYTE], DiskData[TRACK_LEN_HIGH_BYTE]) - TRACK_HEADER_LEN);
                numSides = ((DiskData[FLAGS_BYTE] & SINGLE_SIDED_FLAG) == SINGLE_SIDED_FLAG) ? (byte)1 : (byte)2;
                singleDensitySingleByte = (DiskData[FLAGS_BYTE] & SING_DENS_SING_BYTE_FLAG) == SING_DENS_SING_BYTE_FLAG;
                ignoreDensity = (DiskData[FLAGS_BYTE] & IGNORE_SING_DENS_FLAG) == IGNORE_SING_DENS_FLAG;

                alwaysSingleByte = singleDensitySingleByte || ignoreDensity;

                int diskCursor = DISK_HEADER_LENGTH;

                for (byte i = 0; i < numTracks; i++)
                {
                    for (int j = 0; j < numSides; j++)
                    {
                        if (DiskData.Length < diskCursor + trackLength)
                        {
                            Log.LogMessage(string.Format("Unexpected End to DMK File on Track {0} at byte {1}", i, diskCursor));
                        }
                        else
                        {
                            bool[] doubleDensity = new bool[TRACK_HEADER_LEN / 2];

                            bool needUndoubling = false;
                            for (int k = 0; k < doubleDensity.Length; k++)
                            {
                                ushort header = (ushort)(Lib.CombineBytes(DiskData[diskCursor + k * 2], DiskData[diskCursor + k * 2 + 1]));
                                doubleDensity[k] = ((header & DOUBLE_DENSITY_MASK) == DOUBLE_DENSITY_MASK);
                                needUndoubling |= (header > 0x0001) && !doubleDensity[k];
                            }
                            needUndoubling &= !ignoreDensity && !alwaysSingleByte;
                            byte[] trackData;
                            if (needUndoubling)
                            {
                                trackData = UndoubleTrack(DiskData, diskCursor, trackLength, GetIdamLocations(DiskData, doubleDensity, diskCursor + TRACK_HEADER_LEN, trackLength), doubleDensity);
                            }
                            else
                            {
                                trackData = new byte[trackLength];
                                Array.Copy(DiskData, diskCursor + TRACK_HEADER_LEN, trackData, 0, trackLength);
                            }

                            Track t = SafeGetTrack(i, j > 0);
                            t.Deserialize(trackData, doubleDensity);
                            diskCursor += trackLength + TRACK_HEADER_LEN;
                        }
                    }
                }
                UpdateDoubleDensity();
            }
            catch (Exception ex)
            {
                Log.LogMessage(string.Format("Error deserializing DMK Disk: {0} ", ex));
                Reset();
            }
        }
    }
}
