using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sharp80
{
    internal abstract class Floppy : ISerializable
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
        public const byte FILLER_BYTE = 0x4E;

        public const ushort CRC_RESET = 0xFFFF;
        public const ushort CRC_RESET_A1_A1 = 0x968B;
        public const ushort CRC_RESET_A1_A1_A1 = 0xCDB4;
        public const ushort CRC_RESET_A1_A1_A1_FE = 0xB230;
        public const ushort CRC_RESET_FE = 0xEF21;

        protected List<Track> tracksSide0, tracksSide1;

        protected bool writeProtected;
        protected byte numTracks;
        protected byte numSides;

        public Floppy()
        {
            this.Reset();
        }

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
        public byte NumSides
        {
            get { return numSides; }
        }
        public byte NumTracks
        {
            get { return numTracks; }
        }
        public byte SectorsPerTrack
        {
            get { return (byte)tracksSide0.Concat(tracksSide1).Max(t => t.SectorCount); }
        }
        public bool Changed
        {
            get; protected set;
        }
        public bool Formatted { get { return tracksSide0.Count > 0 && tracksSide0[0].Formatted; } }
        public bool? DoubleDensity
        {
            get;
            protected set;
        }
        public bool WriteProtected
        {
            get { return writeProtected; }
            set
            {
                if (writeProtected != value)
                {
                    Changed = true;
                    writeProtected = value;
                }
            }
        }
        public virtual bool IsEmpty
        {
            get { return numTracks == 0; }
        }
        public FileType OriginalFileType { get; protected set; }

        public static Floppy LoadDisk(string FilePath)
        {
            Floppy f = null;
            byte[] diskData = null;
            try
            {
                diskData = Storage.LoadBinaryFile(FilePath);
                //var bb = Lib.Compress(diskData);
                //var s = String.Join(", ", bb.Select(b => "0x" + b.ToString("X2")));

                f = LoadDisk(diskData, FilePath);
                if (!f.IsEmpty)
                    f.FilePath = FilePath;
            }
            catch (Exception ex)
            {
                Log.LogMessage(string.Format("Error loading floppy: {0}", ex));
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
            return f ?? new DMK();
        }

        public byte HighestSectorNumber(byte TrackNumber, bool SideOne)
        {
            return SafeGetTrack(TrackNumber, SideOne).HighestSectorNumber;
        }
        public byte LowestSectorNumber(byte TrackNumber, bool SideOne)
        {
            return SafeGetTrack(TrackNumber, SideOne).LowestSectorNumber;
        }

        public abstract byte[] Serialize(bool ForceDMK);
        public abstract void WriteTrackData(byte TrackNumber, bool SideOne, byte[] Data, bool[] DoubleDensity);
        public abstract byte[] GetSectorData(byte TrackNumber, bool SideOne, byte SectorNumber);
        public abstract byte GetDAM(byte TrackNumber, bool SideOne, byte SectorNumber);
        public abstract byte[] GetTrackData(byte TrackNumber, bool SideOne);
        public abstract bool IsDoubleDensity(byte TrackNumber, bool SideOne, byte SectorNumber);

        public bool TrackHasIDAMAt(byte TrackNumber, bool SideOne, uint Index, out bool DoubleDensity)
        {
            return SafeGetTrack(TrackNumber, SideOne).HasIDAMAt(Index, out DoubleDensity);
        }

        protected Track SafeGetTrack(byte TrackNumber, bool SideOne)
        {
            var tracks = SideOne ? tracksSide1 : tracksSide0;

            while (TrackNumber >= tracks.Count)
            {
                tracks.Add(new Track(STANDARD_TRACK_LENGTH_DOUBLE_DENSITY, TrackNumber, SideOne));
                numTracks = (byte)Math.Max(numTracks, tracks.Count);
            }

            return tracks[TrackNumber];
        }

        public static byte GetDataLengthCode(ushort DataLength)
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

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            if (this.IsEmpty)
            {
                Writer.Write(0);
                Writer.Write(String.Empty);
            }
            else
            {
                byte[] b = this.Serialize(ForceDMK: true);

                Writer.Write(b.Length);
                Writer.Write(b);
                Writer.Write(this.FilePath);
            }
        }

        public abstract void Deserialize(BinaryReader Reader);

        protected void Reset()
        {
            numSides = 1;
            numTracks = 0;
            writeProtected = false;

            Changed = false;
            FilePath = string.Empty;

            tracksSide0 = new List<Track>();
            tracksSide1 = new List<Track>();
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
   
