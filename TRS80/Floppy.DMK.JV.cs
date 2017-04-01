/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharp80.TRS80
{
    public partial class DMK
    {
        // CONSTANTS

        private const int JV1_SECTORS_PER_TRACK = 10;
        private const int JV1_SECTOR_SIZE = 0x100;

        private const int JV3_SECTORS_PER_HEADER = 2901;
        private const int JV3_HEADER_SIZE = JV3_SECTORS_PER_HEADER * 3;
        private const byte JV3_DOUBLE_DENSITY = 0x80; // 1 = DDEN; 0 = SDEN
        private const byte JV3_DAM_TYPE = 0x60;       // DAM code
        private const byte JV3_SIDE_ONE = 0x10;       // 0 = side 0; 1 = side 1
        private const byte JV3_CRC_ERROR = 0x08;      // 0 = OK; 1 = CRC error */
        private const byte JV3_NON_IBM = 0x04;        // 0=normal
        private const byte JV3_SECTOR_SIZE_MASK = 0x03;  /* in used sectors: 0=256,1=128,2=1024,3=512
                                                            in free sectors: 0=512,1=1024,2=128,3=256 */
        private const byte JV3_SECTOR_FREE = 0xFF;    // in track and sector fields of free sectors */
        private const byte JV3_SECTOR_FREE_FLAGS = 0xFC;  // in flags field, or'd with size code

        // JV3

        public static Floppy FromJV3(byte[] DiskData)
        {
            var sectors = new List<SectorDescriptor>();

            int diskCursor = 0;
            bool? writeProt = null;
            while (diskCursor < DiskData.Length)
            {
                // Read sector Headers
                for (int i = diskCursor; i < diskCursor + JV3_HEADER_SIZE; i += 3)
                {
                    if (DiskData[i] != JV3_SECTOR_FREE)
                    {
                        var sd = new SectorDescriptor()
                        {
                            TrackNumber = DiskData[i],
                            SectorNumber = DiskData[i + 1]
                        };

                        byte flags = DiskData[i + 2];

                        sd.InUse = sd.TrackNumber != JV3_SECTOR_FREE || sd.SectorNumber != JV3_SECTOR_FREE || ((flags & JV3_SECTOR_FREE_FLAGS) != JV3_SECTOR_FREE_FLAGS);
                        sd.DoubleDensity = (flags & JV3_DOUBLE_DENSITY) == JV3_DOUBLE_DENSITY;

                        // The 2-bit DAM_TYPE field encodes the sector's data address mark:
                        // JV3_DAM value   Single density        Double density
                        // 0x00            0xFB (Normal)         0xFB (Normal)
                        // 0x20            0xFA (User-defined)	 0xF8 (Deleted)
                        // 0x40            0xF9 (User-defined)   Invalid; unused
                        // 0x60            0xF8 (Deleted)        Invalid; unused
                        switch (flags & JV3_DAM_TYPE)
                        {
                            case 0x00:
                                sd.DAM = DAM_NORMAL;
                                break;
                            case 0x20:
                                sd.DAM = DAM_DELETED;// don't record 0xFA in SD since causes incompatibilities
                                break;
                            case 0x40:
                                sd.DAM = sd.DoubleDensity ? DAM_NORMAL : (byte)0xF9;
                                break;
                            case 0x60:
                                sd.DAM = DAM_DELETED;
                                break;
                        }

                        sd.SideOne = (flags & JV3_SIDE_ONE) == JV3_SIDE_ONE;
                        sd.CrcError = (flags & JV3_CRC_ERROR) == JV3_CRC_ERROR;

                        // No reason to use this:
                        // sd.NonIbm = (flags & JV3_NON_IBM) == JV3_NON_IBM;

                        // Sector Size Codes
                        // Size    IBM size   SECTOR_SIZE field SECTOR_SIZE field
                        //           code        if in use         if free
                        // 0x080      00            1 	              2
                        // 0x100      01            0                 3
                        // 0x200      02            3                 0
                        // 0x400      03            2                 1

                        // JV3 sector size code is stored in a weird way
                        sd.SectorSizeCode = (byte)((flags & JV3_SECTOR_SIZE_MASK) ^ (sd.InUse ? 1 : 2));

                        sd.SectorSize = GetDataLengthFromCode(sd.SectorSizeCode);
                        sectors.Add(sd);
                    }
                }

                diskCursor += JV3_HEADER_SIZE;

                if (writeProt.HasValue)
                    diskCursor++;
                else
                    writeProt = (DiskData[diskCursor++] != 0xFF);

                int q = 0;
                foreach (var sd in sectors)
                {
                    q++;
                    if (sd.InUse)
                    {
                        if (DiskData.Length - diskCursor < sd.SectorSize) // not enough data for sector
                        {
                            if (DiskData.Length > diskCursor) // try to get some data
                                sd.SectorData = DiskData.Slice(diskCursor, DiskData.Length);
                            else
                                sd.SectorData = new byte[sd.SectorSize];
                            diskCursor = DiskData.Length;
                        }
                        else
                        {
                            sd.SectorData = DiskData.Slice(diskCursor, diskCursor + sd.SectorSize);
                        }
                    }
                    diskCursor += sd.SectorSize;
                }
            }
            return DMK.MakeFloppy(Sectors: sectors,
                                    WriteProtected: writeProt.Value,
                                    OriginalFileType: FileType.JV3);
        }
        private byte[] SerializeToJV3()
        {
            var sectors = tracks.SelectMany(t => t.ToSectorDescriptors())
                                .OrderBy(s => s.SideOne)
                                .OrderBy(s => s.TrackNumber)
                                .ToList();

            byte[] temp = new byte[JV3_HEADER_SIZE * (int)(Math.Ceiling((double)sectors.Count / (double)JV3_SECTORS_PER_HEADER)) + 2 + this.NumTracks * MAX_TRACK_LENGTH];

            int cursor = 0;
            int sectorCount = 0;
            foreach (var s in sectors)
            {
                if (sectorCount % JV3_SECTORS_PER_HEADER == 0)
                {
                    for (int i = 0, j = sectorCount; i < JV3_SECTORS_PER_HEADER; i++, j++)
                    {
                        if (j < sectors.Count)
                        {
                            temp[cursor++] = sectors[j].TrackNumber;
                            temp[cursor++] = sectors[j].SectorNumber;

                            var ss = sectors[j];
                            if (ss.DoubleDensity)
                                temp[cursor] = JV3_DOUBLE_DENSITY;
                            else
                                temp[cursor] = 0x00;

                            if (ss.SideOne)
                                temp[cursor] |= JV3_SIDE_ONE;

                            if (ss.CrcError)
                                temp[cursor] |= JV3_CRC_ERROR;

                            switch (ss.DAM)
                            {
                                case DAM_DELETED:
                                    temp[cursor] |= (ss.DoubleDensity ? (byte)0x20 : (byte)0x60);
                                    break;
                                case 0xF9:
                                    temp[cursor] |= 0x40;
                                    break;
                                case 0xFA:
                                    temp[cursor] |= 0x20;
                                    break;
                                case DAM_NORMAL: // 0x00
                                    break;
                                default:
                                    break;
                            }
                            cursor++;
                        }
                        else
                        {
                            temp[cursor++] = JV3_SECTOR_FREE;
                            temp[cursor++] = JV3_SECTOR_FREE;
                            temp[cursor++] = JV3_SECTOR_FREE_FLAGS | 0x03;
                        }
                    }
                }
                Array.Copy(s.SectorData, 0, temp, cursor, s.SectorSize);
                cursor += s.SectorSize;
                sectorCount++;
            }
            byte[] data = new byte[cursor];
            Array.Copy(temp, data, cursor);
            return data;
        }

        // JV1

        public static Floppy FromJV1(byte[] DiskData)
        {
            const byte SECTORS_PER_TRACK = 10;
            const int SECTOR_LENGTH = 0x100;

            if (DiskData.Length % (SECTOR_LENGTH * SECTORS_PER_TRACK) != 0)
                throw new Exception("Invalid JV1 Format");

            var sectors = new List<SectorDescriptor>();

            byte numTracks = (byte)(DiskData.Length / 0x100 / 0x0A);

            // Sectors not interleaved

            for (byte i = 0; i < numTracks; i++)
                for (byte j = 0; j < SECTORS_PER_TRACK; j++)
                {
                    var sd = new SectorDescriptor()
                    {
                        TrackNumber = i,
                        SectorNumber = j,
                        CrcError = false,
                        DAM = (i == 17) ? DAM_DELETED : DAM_NORMAL,
                        DoubleDensity = false,
                        InUse = true,
                        //NonIbm = false,
                        SectorSize = SECTOR_LENGTH,
                        SectorSizeCode = GetDataLengthCode(SECTOR_LENGTH),
                        SideOne = false,
                        SectorData = new byte[SECTOR_LENGTH]
                    };
                    Array.Copy(DiskData, (i * SECTORS_PER_TRACK + j) * SECTOR_LENGTH, sd.SectorData, 0, SECTOR_LENGTH);
                    sectors.Add(sd);
                }

            return MakeFloppy(Sectors: sectors,
                              WriteProtected: false,
                              OriginalFileType: FileType.JV1);
        }
        private byte[] SerializeToJV1()
        {
            var sectors = tracks.Where(t => !t.SideOne)
                                .SelectMany(t => MakeJV1Compatible(t.ToSectorDescriptors()))
                                .OrderBy(s => s.SectorNumber)
                                .OrderBy(s => s.TrackNumber);

            byte[] data = new byte[tracks.Count() * JV1_SECTORS_PER_TRACK * JV1_SECTOR_SIZE];

            int cursor = 0;
            foreach (var s in sectors)
            {
                Array.Copy(s.SectorData, 0, data, cursor, JV1_SECTOR_SIZE);
                cursor += JV1_SECTOR_SIZE;
            }
            return data;
        }
        private IEnumerable<SectorDescriptor> MakeJV1Compatible(IEnumerable<SectorDescriptor> Sectors)
        {
            var sectors = Sectors.Where(s => s.SectorSize == JV1_SECTOR_SIZE);

            var newSectors = new List<SectorDescriptor>();

            for (int i = 0; i < JV1_SECTORS_PER_TRACK; i++)
                newSectors.Add(sectors.FirstOrDefault(s => s.SectorNumber == i) ?? new SectorDescriptor() { SectorData = new byte[JV1_SECTOR_SIZE] });

            return newSectors;
        }
    }
}