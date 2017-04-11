/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace Sharp80.TRS80
{
    public enum FloppyFileType { DMK, JV1, JV3 }

    public partial class Floppy : IFloppy
    {
        public const int MAX_TRACK_LENGTH = 0x3980;
        public const int STANDARD_TRACK_LENGTH_DOUBLE_DENSITY = 0x1880;
        public const int STANDARD_TRACK_LENGTH_SINGLE_DENSITY = 0x0E00;

        private FloppyData floppyData = null;

        // CONSTRUCTORS

        internal Floppy()
        {

        }
        public Floppy(string FilePath)
        {
            this.FilePath = FilePath;

            if (IO.LoadBinaryFile(FilePath, out byte[] diskData))
                LoadDisk(diskData, FilePath);
        }
        public Floppy(bool Formatted)
        {
            if (Formatted)
            {
                floppyData = new FloppyData(true);
                FilePath = Storage.FILE_NAME_NEW;
            }
            else
            {
                floppyData = new FloppyData(false);
                FilePath = Storage.FILE_NAME_UNFORMATTED;
            }
            OriginalFileType = FloppyFileType.DMK;
        }
        public Floppy(byte[] DiskData, string FilePath)
        {
            LoadDisk(DiskData, FilePath);
        }
        internal Floppy(IEnumerable<SectorDescriptor> Sectors, bool WriteProtection)
        {
            floppyData = new FloppyData(Sectors, WriteProtection);
            FilePath = String.Empty;
        }

        // PROPERTIES

        public string FilePath { get; set; } = String.Empty;
        public string FileDisplayName
        {
            get
            {
                switch (FilePath)
                {
                    case Storage.FILE_NAME_NEW:
                        return "<NEW>";
                    case Storage.FILE_NAME_UNFORMATTED:
                        return "<UNFORMATTED>";
                    case Storage.FILE_NAME_TRSDOS:
                        return "<TRSDOS>";
                    default:
                        if (Storage.IsLibraryFile(FilePath))
                            return Path.GetFileNameWithoutExtension(FilePath).ToUpper();
                        else
                            return FilePath;
                }
            }
        }
        public bool Valid => floppyData != null && floppyData.NumTracks > 0;
        public FloppyFileType OriginalFileType;

        // IO

        private void LoadDisk(byte[] diskData, string FilePath)
        {
            int fileLength = diskData.Length;

            if (fileLength > 0)
            {
                switch (Path.GetExtension(FilePath).ToLower())
                {
                    case ".dmk":
                        floppyData = new FloppyData(diskData);
                        OriginalFileType = FloppyFileType.DMK;
                        break;
                    case ".jv1":
                        floppyData = FromJV1(diskData);
                        OriginalFileType = FloppyFileType.JV1;
                        break;
                    case ".jv3":
                        floppyData = FromJV3(diskData);
                        OriginalFileType = FloppyFileType.JV3;
                        break;
                    default:
                        // Probably a .dsk extension. Use heuristic to figure
                        // out what kind of disk it is. Probably could be improved.
                        if ((fileLength % 2560) == 0)
                        {
                            // JV1
                            floppyData = FromJV1(diskData);
                            OriginalFileType = FloppyFileType.JV1;
                        }
                        else if (diskData[0x0C] == 0 &&
                                diskData[0x0D] == 0 &&
                                diskData[0x0E] == 0 &&
                                diskData[0x0F] == 0)
                        {
                            // DMK
                            floppyData = new FloppyData(diskData);
                            OriginalFileType = FloppyFileType.DMK;
                        }
                        else
                        {
                            // JV3
                            floppyData = FromJV3(diskData);
                            OriginalFileType = FloppyFileType.JV3;
                        }
                        break;
                }
                this.FilePath = FilePath;
            }
        }
        public bool Save(FloppyFileType Type)
        {
            if (floppyData is null)
                return false;

            var bytes = floppyData.Serialize();

            switch (Type)
            {
                case FloppyFileType.DMK:
                    IO.SaveBinaryFile(FilePath, bytes);
                    return true;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Used to create a new floppy file with a file on it (such as putting a CMD
        /// or BAS file on an otherwise blank floppy.)
        /// </summary>
        /// <returns>True on success</returns>
        public static bool FromFile(string FilePath, out string FloppyPath)
        {
            FloppyPath = String.Empty;
            if (IO.LoadBinaryFile(FilePath, out byte[] bytes))
            {
                var f = MakeFloppyFromFile(bytes, Path.GetFileName(FilePath));
                if (f.Valid)
                {
                    FloppyPath = f.FilePath = FilePath.ReplaceExtension("dsk");
                    return f.Save(FloppyFileType.DMK);
                }
            }
            return false;
        }

        // FLOPPYDATA PASS THROUGH

        public bool WriteProtected
        {
            get => floppyData?.WriteProtected ?? true;
            set
            {
                if (!(floppyData is null))
                    floppyData.WriteProtected = value;
            }
        }
        public bool DoubleSided => floppyData?.DoubleSided ?? false;
        public bool Changed => floppyData?.Changed ?? false;
        public bool Formatted => floppyData?.Formatted ?? false;
        public byte NumTracks => floppyData?.NumTracks ?? 0;
        public ITrack GetTrack(int TrackNum, bool SideOne) => floppyData?.GetTrack(TrackNum, SideOne);
        public byte SectorCount(byte TrackNum, bool SideOne) => floppyData?.SectorCount(TrackNum, SideOne) ?? 0;
        public SectorDescriptor GetSectorDescriptor(byte TrackNum, bool SideOne, byte SectorIndex) => floppyData?.GetSectorDescriptor(TrackNum, SideOne, SectorIndex);

        // SERIALIZATION

        public void Serialize(BinaryWriter Writer)
        {
            throw new NotImplementedException();
        }
        public bool Deserialize(BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                int dataLength = Reader.ReadInt32();
                if (dataLength > 0)
                {
                    var bytes = Reader.ReadBytes(dataLength);
                    floppyData = new FloppyData(bytes);
                }
                else
                {
                    floppyData = null;
                }
                FilePath = Reader.ReadString();
                return Valid;
            }
            catch
            {
                return false;
            }
        }
    }
}
