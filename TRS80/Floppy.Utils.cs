﻿/// Sharp 80 (c) Matthew Hamilton
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

            fn += ext;

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

            for (byte trk = 0; trk < NumTracks; trk++)
                for (byte side = 0; side < numSides; side++)
                {
                    byte secNum = startingSectorNumber;
                    for (byte secIdx = 0; secIdx < sectorsPerTrack; secIdx++)
                    {
                        byte[] sectorData;

                        if (DoubleDensity)
                            sectorData = GetFormattedBytes(trk, side, secNum);
                        else
                            sectorData = new byte[SECTOR_SIZE].Fill((byte)0xF5);

                        sectors.Add(new SectorDescriptor(trk, secNum, side > 0, DoubleDensity, DAM_NORMAL, sectorData));

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
            return sectors;
        }
        private static byte[] GetFormattedBytes(byte TrackNum, byte SideNum, byte SectorNum)
        {
            const byte DIRECTORY_TRACK = 17;

            if (TrackNum == 0 && SideNum == 0 && SectorNum == 1)
                return new byte[]
                    {0xFE,0x11,0x3E,0xD0,0xD3,0xF0,0x21,0x02,0x00,0x22,0xEA,0x43,0xAF,0x32,0xEC,0x43,0xCD,0x3E,0x43,0xFE,0x01,0x28,0x0C,0xFE,0x02,0x20,0xE7,0xCD,0x3E,0x43,0xCD,0x35,0x43,0xE9,0xFF,0xCD,0x3E,0x43,0xD6,0x02,0x47,0xCD,0x35,0x43,0xCD,0x3E,0x43,0x77,0x23,0x10,0xF9,0x18,0xDB,0xCD,0x3E,0x43,0x6F,0xCD,0x3E,0x43,0x67,0xC9,0xC5,0xE5,0x3A,0xEC,0x43,0xB7,0x20,0x2E,0x06,0x09,0xC5,0xCD,0x7F,0x43,0xC1,0xE6,0x1D,0x28,0x13,0x3E,0xD0,0xD3,0xF0,0x10,0xF1,0x3E,0x17,0xCD,0x33,0x00,0x21,0xED,0x43,0xCD,0x1B,0x02,0x18,0xFE,0x2A,0xEA,0x43,0x2C,0x7D,0xFE,0x13,0x38,0x03,0x2E,0x01,0x24,0x22,0xEA,0x43,0xAF,0x6F,0x26,0x4D,0x3C,0x32,0xEC,0x43,0x7E,0xE1,0xC1,0xC9,0xCD,0xC5,0x43,0x01,0xF3,0x00,0x3E,0x81,0xD3,0xF4,0x57,0x21,0xB7,0x43,0x22,0x4A,0x40,0x3E,0xC3,0x32,0x49,0x40,0xF3,0x3E,0xC0,0xD3,0xE4,0x1E,0x02,0x21,0x00,0x4D,0x3E,0x84,0xD3,0xF0,0xCD,0xE0,0x43,0xDB,0xF0,0xA3,0x28,0xFB,0xED,0xA2,0x7A,0xF6,0x40,0xD3,0xF4,0xED,0xA2,0xC3,0xB0,0x43,0xE1,0xAF,0xD3,0xE4,0x3E,0x81,0xD3,0xF4,0xCD,0xE6,0x43,0xDB,0xF0,0xC9,0x3E,0x81,0xD3,0xF4,0x2A,0xEA,0x43,0x7C,0xD3,0xF3,0x3E,0x1C,0xD3,0xF0,0xCD,0xE6,0x43,0xDB,0xF0,0xCB,0x47,0x20,0xFA,0x7D,0xD3,0xF2,0xC9,0xF5,0xF1,0xF5,0xF1,0xF5,0xF1,0xF5,0xF1,0x00,0xC9,0x02,0x00,0x00,0x17,0x45,0x52,0x52,0x4F,0x52,0x0D,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x13,0x28};
            else if (TrackNum == 0 && SideNum == 0 && SectorNum == 2)
                return new byte[]
                    { 0x01, 0x21, 0x00, 0x4D, 0x3E, 0x17, 0xCD, 0x33, 0x00, 0x21, 0x0D, 0x4D, 0xCD, 0x1B, 0x02, 0x18, 0xFE, 0x4E, 0x6F, 0x74, 0x20, 0x61, 0x20, 0x53, 0x59, 0x53, 0x54, 0x45, 0x4D, 0x20, 0x44, 0x69, 0x73, 0x6B, 0x0D, 0x02, 0x02, 0x00, 0x4D, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5, 0xE5 };
            else if (TrackNum == DIRECTORY_TRACK && SideNum == 0 && SectorNum == 1)
                return new byte[]
                    {0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0xEF,0x5C,0x54,0x52,0x53,0x44,0x4F,0x53,0x31,0x33,0x30,0x31,0x2F,0x30,0x31,0x2F,0x38,0x39,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};
            else if (TrackNum == DIRECTORY_TRACK && SideNum == 0 && SectorNum ==2)
                return new byte[]
                    {0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF};
            else if (TrackNum == DIRECTORY_TRACK)
                return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x63, 0x29, 0x20, 0x31, 0x39, 0x38, 0x30, 0x20, 0x54, 0x61, 0x6E, 0x64, 0x79, 0x20, 0x20 };
            else
                return new byte[]
                    {0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0xE5,0x28,0x63,0x29,0x20,0x31,0x39,0x38,0x30,0x20,0x54,0x61,0x6E,0x64,0x79,0x20,0x20};
        }
    }
}
