/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sharp80.TRS80
{
    public partial class Floppy : IFloppy
    {
        public const ushort CRC_RESET = 0xFFFF;
        public const ushort CRC_RESET_A1_A1 = 0x968B;
        public const ushort CRC_RESET_A1_A1_A1 = 0xCDB4;
        public const ushort CRC_RESET_A1_A1_A1_FE = 0xB230;
        public const ushort CRC_RESET_FE = 0xEF21;

        public const byte DAM_NORMAL = 0xFB;
        public const byte DAM_DELETED = 0xF8;

        public const byte IDAM = 0xFE;

        public const byte FILLER_BYTE_DD = 0x4E;
        public const byte FILLER_BYTE_SD = 0xFF;

        internal static byte GetDataLengthCode(int DataLength)
        {
            switch (DataLength)
            {
                case 0x080: return 0x00;
                case 0x100: return 0x01;
                case 0x200: return 0x02;
                case 0x400: return 0x03;
                default: return 0x01;
            }
        }
        internal static ushort GetDataLengthFromCode(byte DataLengthCode)
        {
            switch (DataLengthCode & 0x03)
            {
                case 0x00: return 0x080;
                case 0x01: return 0x100;
                case 0x02: return 0x200;
                case 0x03: return 0x400;
                default: return 0x000; // Impossible
            }
        }
        internal static Floppy MakeFloppyFromFile(byte[] b, string filename)
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
            return new Floppy(sectors, false);
        }

        // HELPERS

        private static string ConvertWindowsFilePathToTRSDOSFileName(string WinPath)
        {
            string ext = Path.GetExtension(WinPath);

            if (ext.StartsWith("."))
                ext = ext.Substring(1);

            if (ext.Length > 3)
                ext = ext.Substring(0, 3);

            ext = ext.ToUpper();

            string fn = Path.GetFileNameWithoutExtension(WinPath);
            if (fn.Length > 8)
                fn = fn.Substring(0, 8);

            fn = fn.ToUpper();

            for (int i = 1; i < fn.Length; i++)
            {
                if (!IsValidTrsdosChar(fn[i], i == 0))
                    fn = fn.Substring(0, i) + "X" + fn.Substring(i + 1);
            }
            for (int i = 0; i < ext.Length; i++)
            {
                if (!IsValidTrsdosChar(fn[i], true))
                    ext = ext.Substring(0, i) + "X" + ext.Substring(i + 1);
            }

            fn = fn.PadRight(8);
            ext = ext.PadRight(3, 'X');

            fn = fn + ext;

            return fn;
        }
        private static bool IsValidTrsdosChar(char c, bool IsFirstChar)
        {
            if (IsFirstChar)
                return c.IsBetween('A', 'Z');
            else
                return c.IsBetween('A', 'Z') || c.IsBetween('0', '9');
        }
        private static byte HashFilename(string Filename)
        {
            /* ASSEMBLY HASH ALGORITHM
              
                HASHNAME	EQU	$
	                        LD	B,11		;Init for 11 chars
	                        XOR	A		;Clear for start
                HNAME1	XOR	(HL)		;Modulo 2 addition
	                        INC	HL		;Bump to next character
	                        RLCA			;Rotate bit structure
	                        DJNZ	HNAME1		;  & loop for field len
	                        OR	A		;Do not permit a zero
	                        JR	NZ,HNAME2	;  hash code
	                        INC	A
                        HNAME2	LD	(FILEHASH),A	;Stuff code for later
	                        RET
             */

            if (Filename.Length != 11)
                throw new Exception();

            byte a = 0;
            for (int i = 0; i < 11; i++)
            {
                a ^= (byte)Filename[i];
                a = (byte)((a << 1) | (a >> 7)); // rlca
            }
            if (a == 0)
                a = 1;
            return a;
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
                        var sectorData = new byte[SECTOR_SIZE];

                        if (i != DIRECTORY_TRACK)
                            sectorData.Fill((byte)0xE5);

                        sectors.Add(new SectorDescriptor(i, secNum, j > 0, DoubleDensity, DAM_NORMAL, sectorData));

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
    }
}
