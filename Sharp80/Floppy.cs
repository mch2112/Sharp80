/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sharp80
{
    internal abstract class Floppy : ISerializable, IFloppy
    {
        public enum FileType { DMK, JV1, JV3 }

        public const byte MAX_TRACKS = 80;
        public const int STANDARD_TRACK_LENGTH_DOUBLE_DENSITY = 0x1880;
        public const int STANDARD_TRACK_LENGTH_SINGLE_DENSITY = 0x0E00;
        public const int MAX_TRACK_LENGTH = 0x3980;
        public const int MAX_SECTORS_PER_TRACK = 0x40;
        public const byte IDAM = 0xFE;
        public const byte DAM_NORMAL = 0xFB;
        public const byte DAM_DELETED = 0xF8;
        public const byte FILLER_BYTE_DD = 0x4E;
        public const byte FILLER_BYTE_SD = 0xFF;

        public const ushort CRC_RESET = 0xFFFF;
        public const ushort CRC_RESET_A1_A1 = 0x968B;
        public const ushort CRC_RESET_A1_A1_A1 = 0xCDB4;
        public const ushort CRC_RESET_A1_A1_A1_FE = 0xB230;
        public const ushort CRC_RESET_FE = 0xEF21;

        protected List<Track> tracks;

        protected bool writeProtected;

        protected Floppy() { }
        public Floppy(byte[] Data) { throw new Exception("Need to deserialize"); }
        public Floppy(BinaryReader Reader)
            : this(Reader.ReadInt32(), Reader)
        {
        }
        private Floppy(int DataLength, BinaryReader Reader)
            : this(Reader.ReadBytes(DataLength))
        {
            FilePath = Reader.ReadString();
        }
        public string FilePath { get; set; }
        public bool Formatted { get { return tracks.Any(t => t.Formatted); } }

        private bool changed = false;
        public bool Changed
        { get { return changed || tracks.Any(t => t.Changed); } }

        public bool WriteProtected
        {
            get { return writeProtected; }
            set
            {
                if (writeProtected != value)
                {
                    changed = true;
                    writeProtected = value;
                }
            }
        }
        public byte NumTracks
        {
            get { return (byte)(tracks.Max(t => t.PhysicalTrackNum) + 1); }
        }
        public bool DoubleSided
        {
            get { return tracks.Any(t => t.SideOne); }
        }
        public FileType OriginalFileType { get; protected set; }
        public abstract SectorDescriptor GetSectorDescriptor(byte TrackNum, bool SideOne, byte SectorIndex);
        public abstract byte SectorCount(byte TrackNumber, bool SideOne);
        public static Floppy LoadDisk(string FilePath)
        {
            Floppy f;

            if (Storage.LoadBinaryFile(FilePath, out byte[] diskData))
            {
                f = LoadDisk(diskData, FilePath);
                if (f != null)
                    f.FilePath = FilePath;
            }
            else
            {
                Log.LogDebug("Error loading floppy");
                f = null;
            }

            return f;
        }

        private static Floppy LoadDisk(byte[] diskData, string FilePath)
        {
            Floppy f = null;
            int fileLength = diskData.Length;

            if (fileLength > 0)
            {
                switch (Path.GetExtension(FilePath).ToLower())
                {
                    case ".dmk":
                        f = new DMK(diskData);
                        break;
                    case ".jv1":
                        f = DMK.FromJV1(diskData);
                        break;
                    case ".jv3":
                        f = DMK.FromJV3(diskData);
                        break;
                    default:
                        // Probably a .dsk extension. Use heuristic to figure
                        // out what kid of disk it is. Probably could be improved.
                        if ((fileLength % 2560) == 0)
                            // JV1
                            f = DMK.FromJV1(diskData);
                        else if (diskData[0x0C] == 0 &&
                                 diskData[0x0D] == 0 &&
                                 diskData[0x0E] == 0 &&
                                 diskData[0x0F] == 0)
                            f = new DMK(diskData);
                        else
                            // JV3
                            f = DMK.FromJV3(diskData);
                        break;
                }
            }
            return f;
        }

        public abstract byte[] Serialize(bool ForceDMK);
        //public abstract void WriteTrackData(byte TrackNumber, bool SideOne, byte[] Data, bool[] DoubleDensity);
        //public abstract byte[] GetSectorData(byte TrackNumber, bool SideOne, byte SectorNumber);
        //public abstract SectorDescriptor GetSectorDescriptor(byte TrackNumber, bool SideOne, byte SectorNumber);
        //public abstract byte[] GetTrackData(byte TrackNumber, bool SideOne);
        //public abstract bool IsDoubleDensity(byte TrackNumber, bool SideOne, byte SectorNumber);

        public abstract Track GetTrack(int TrackNum, bool SideOne);

        public static byte GetDataLengthCode(int DataLength)
        {
            switch (DataLength)
            {
                case 0x080:  return 0x00;
                case 0x100: return 0x01;
                case 0x200: return 0x02;
                case 0x400: return 0x03;
                default:    return 0x01;
            }
        }
        public static ushort GetDataLengthFromCode(byte DataLengthCode)
        {
            switch (DataLengthCode & 0x03)
            {
                case 0x00: return 0x080;
                case 0x01: return 0x100;
                case 0x02: return 0x200;
                case 0x03: return 0x400;
                default:   return 0x000; // Impossible
            }
        }

        public void Serialize(BinaryWriter Writer)
        {
            byte[] b = Serialize(ForceDMK: true);

            Writer.Write(b.Length);
            Writer.Write(b);
            Writer.Write(FilePath);
        }

        public abstract void Deserialize(BinaryReader Reader);

        protected void Reset()
        {
            writeProtected = false;

            changed = false;
            FilePath = string.Empty;

            tracks = new List<Track>();
        }
        protected static string ConvertWindowsFilePathToTRSDOSFileName(string WinPath)
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
        protected static bool IsValidTrsdosChar(char c, bool IsFirstChar)
        {
            if (IsFirstChar)
                return c >= 'A' && c <= 'Z';
            else
                return (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }
        protected static byte HashFilename(string Filename)
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
    }
}
   
