/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;

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
        
        public DMK(byte[] DiskData) => Deserialize(DiskData);
        
        public DMK(System.IO.BinaryReader Reader) => Deserialize(Reader, Computer.SERIALIZATION_VERSION);

        public DMK(bool Formatted)
        {
            if (Formatted)
            {
                Deserialize(SectorsToDmkBytes(GetDiskSectorDescriptors(NumTracks: 40,
                                                                       DoubleSided: true,
                                                                       DoubleDensity: true),
                                              false));
                FilePath = Storage.FILE_NAME_NEW;
            }
            else
            {
                Deserialize(UnformattedDmkBytes(40, true));
                WriteProtected = false;
                FilePath = Storage.FILE_NAME_UNFORMATTED;
            }
            OriginalFileType = FileType.DMK;
        }

        public static bool FromFile(string FilePath, out string NewPath)
        {
            if (IO.LoadBinaryFile(FilePath, out byte[] bytes))
            {
                byte[] diskImage = MakeFloppyFromFile(bytes, System.IO.Path.GetFileName(FilePath)).Serialize(ForceDMK: true);
                if (diskImage.Length > 0)
                    return IO.SaveBinaryFile(NewPath = FilePath.ReplaceExtension("dsk"), diskImage);
            }
            NewPath = String.Empty;
            return false;
        }
        private static Floppy MakeFloppyFromFile(byte[] b, string filename)
        {
            var fn = ConvertWindowsFilePathToTRSDOSFileName(filename);

            byte neededSectors = (byte)Math.Ceiling((double)b.Length / (double)0x100);
            byte neededGranules = (byte)Math.Ceiling((double)neededSectors / (double)0x003);
            byte neededTracks = (byte)Math.Ceiling((double)neededGranules / (double)0x006);

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
            dirSector.SectorData[20] = neededSectors;
            dirSector.SectorData[21] = 0;

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
        public override Track GetTrack(int TrackNum, bool SideOne)
        {
            return tracks.FirstOrDefault(t => t.PhysicalTrackNum == TrackNum && t.SideOne == SideOne);
        }
        public override SectorDescriptor GetSectorDescriptor(byte TrackNum, bool SideOne, byte SectorIndex)
        {
            return GetTrack(TrackNum, SideOne)?.GetSectorDescriptor(SectorIndex);
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
        public override byte[] Serialize(bool ForceDMK)
        {
            if (ForceDMK)
                return SerializeToDMK();
            else
                switch (OriginalFileType)
                {
                    // Currently we're not writing back to JV1 or JV3 format. Change the extension and
                    // leave the original file in this case.
                    case FileType.JV1:
                    case FileType.JV3:
                        if (FilePath.ToLower().EndsWith(".dsk"))
                            FilePath = FilePath.ReplaceExtension(".dmk");
                        else
                            FilePath = FilePath.ReplaceExtension(".dsk");
                        return SerializeToDMK();
                        //return SerializeToJV1();
                        //return SerializeToJV3();
                    default:
                        return SerializeToDMK();
                }
        }
        
        public override byte SectorCount(byte TrackNumber, bool SideOne)
        {
            return tracks.FirstOrDefault(t => t.PhysicalTrackNum == TrackNumber && t.SideOne == SideOne)?.NumSectors ?? 0;
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
                        var sd = new SectorDescriptor()
                        {
                            TrackNumber = i,
                            SectorNumber = secNum,
                            SideOne = j > 0,
                            InUse = true,
                            SectorSize = SECTOR_SIZE,
                            SectorSizeCode = GetDataLengthCode(SECTOR_SIZE),
                            SectorData = new byte[SECTOR_SIZE],
                            DAM = DAM_NORMAL,
                            DoubleDensity = DoubleDensity,
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
            var f = new DMK(SectorsToDmkBytes(Sectors, WriteProtected))
            {
                OriginalFileType = OriginalFileType
            };
            return f;
        }

        private static byte[] SectorsToDmkBytes(List<SectorDescriptor> Sectors, bool WriteProtected)
        {
            byte numTracks = (byte)(Sectors.Max(s => s.TrackNumber) + 1);
            bool doubleSided = Sectors.Exists(s => s.SideOne);
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

        private static DMK MakeUnformattedFloppy(byte NumTracks, bool DoubleSided) => new DMK(UnformattedDmkBytes(NumTracks, DoubleSided));

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

        private byte[] SerializeToDMK()
        {
            int trackLength = tracks.Max(t => t.LengthWithHeader);
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
                    var t = tracks.FirstOrDefault(tt => tt.PhysicalTrackNum == i && tt.SideOne == (j == 1));
                    if (t == null)
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
        public override bool Deserialize(System.IO.BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                int dataLength = Reader.ReadInt32();
                Deserialize(Reader.ReadBytes(dataLength));
                FilePath = Reader.ReadString();
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void Deserialize(byte[] DiskData)
        {
            try
            {
                Reset();

                if (DiskData.Length < 0x200)
                {
                    Log.LogDebug($"Invalid DMK format: Too short ({DiskData.Length} bytes)");
                    return;
                }

                WriteProtected = DiskData[WRITE_PROTECT_BYTE] == WRITE_PROTECT_VAL;
                ushort trackLength = Lib.CombineBytes(DiskData[TRACK_LEN_LOW_BYTE], DiskData[TRACK_LEN_HIGH_BYTE]);
                int numSides = ((DiskData[FLAGS_BYTE] & SINGLE_SIDED_FLAG) == SINGLE_SIDED_FLAG) ? 1 : 2;

                singleDensitySingleByte = (DiskData[FLAGS_BYTE] & SING_DENS_SING_BYTE_FLAG) == SING_DENS_SING_BYTE_FLAG;
                ignoreDensity = (DiskData[FLAGS_BYTE] & IGNORE_SING_DENS_FLAG) == IGNORE_SING_DENS_FLAG;
                alwaysSingleByte = singleDensitySingleByte || ignoreDensity;

                if (ignoreDensity)
                    throw new NotImplementedException("Need to handle DMK ignore density mode");

                int diskCursor = DISK_HEADER_LENGTH;

                byte trackNum = 0;
                while (diskCursor < DiskData.Length)
                {
                    for (int sideNum = 0; sideNum < numSides; sideNum++)
                    {
                        if (DiskData.Length < diskCursor + trackLength)
                        {
                            Log.LogDebug($"Unexpected End to DMK File on Track {trackNum} at byte {diskCursor}");
                        }
                        else
                        {
                            tracks.Add(new Track(trackNum, sideNum == 1, DiskData.Slice(diskCursor, diskCursor + trackLength), singleDensitySingleByte));
                            diskCursor += trackLength;
                        }
                    }
                    trackNum++;
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, "Error deserializing DMK Disk");
                Reset();
            }
        }
    }
}
