/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sharp80
{
    internal static class Storage
    {
        public static bool LoadBinaryFile(string FilePath, out byte[] Bytes)
        {
            try
            {
                Bytes = File.ReadAllBytes(FilePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogException(ex);
                Bytes = null;
                return false;
            }
        }
        public static List<string> LoadTextFile(string FilePath)
        {
            return new List<string>(File.ReadAllLines(FilePath));
        }
        public static bool SaveBinaryFile(string FilePath, byte[] Data)
        {
            try
            {
                File.WriteAllBytes(FilePath, Data);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogDebug(string.Format("Exception saving file {0}: {1}", FilePath, ex));
                return false;
            }
        }
        public static void SaveTextFile(string FilePath, IEnumerable<string> Lines)
        {
            File.WriteAllLines(FilePath, Lines);
        }
        public static void SaveTextFile(string FilePath, string Text)
        {
            File.WriteAllText(FilePath, Text);
        }
        public static string GetDefaultDriveFileName(byte DriveNum)
        {
            string fileName = String.Empty;

            switch (DriveNum)
            {
                case 0:
                    fileName = Settings.Disk0Filename;
                    break;
                case 1:
                    fileName = Settings.Disk1Filename;
                    break;
                case 2:
                    fileName = Settings.Disk2Filename;
                    break;
                case 3:
                    fileName = Settings.Disk3Filename;
                    break;
            }
            if (File.Exists(fileName) || Floppy.IsFileNameToken(fileName))
                return fileName;
            else
                return String.Empty;
        }

        /// <summary>
        /// Should be called when the floppy is put in the drive. Note that the
        /// floppy's file path may be empty but we may want to save a token
        /// value like {{BLANK}}
        /// </summary>
        public static void SaveDefaultDriveFileName(byte DriveNum, string FilePath)
        {
            switch (DriveNum)
            {
                case 0:
                    Settings.Disk0Filename = FilePath;
                    break;
                case 1:
                    Settings.Disk1Filename = FilePath;
                    break;
                case 2:
                    Settings.Disk2Filename = FilePath;
                    break;
                case 3:
                    Settings.Disk3Filename = FilePath;
                    break;
            }
        }
        public static Floppy MakeBlankFloppy(bool Formatted)
        {
            var f = DMK.MakeBlankFloppy(NumTracks: 40,
                                           DoubleSided: true,
                                           Formatted: Formatted);
            f.FilePath = Formatted ? Floppy.FILE_NAME_BLANK : Floppy.FILE_NAME_UNFORMATTED;

            return f;
        }
        public static void SaveCMDFile(string Title, string FilePath, ushort[] Origin, byte[][] Data, ushort TransferAddress)
        {
            var writer = new BinaryWriter(File.Open(FilePath, FileMode.Create));

            ushort dest;
            int cursor;
            byte lowDest;
            byte highDest;
            int segmentSize;
            int blockSize;

            writer.Write((byte)0x05);
            writer.Write((byte)Title.Length);
            for (int i = 0; i < Title.Length; i++)
                writer.Write((byte)Title[i]);

            for (int i = 0; i < Origin.Length; i++)
            {
                dest = Origin[i];
                cursor = 0;

                segmentSize = Data[i].Length;

                while (cursor < segmentSize)
                {
                    blockSize = Math.Min(0x100, Data[i].Length - cursor);
                    writer.Write((byte)0x01);   // block marker
                    writer.Write((byte)(blockSize + 2)); // 0x02 == 256 bytes
                    dest.Split(out lowDest, out highDest);
                    writer.Write(lowDest);
                    writer.Write(highDest);
                    while (blockSize-- > 0)
                    {
                        writer.Write(Data[i][cursor++]);
                        dest++;
                    }
                }
            }
            writer.Write((byte)0x02);  // transfer address marker
            writer.Write((byte)0x02);  // transfer address length
            TransferAddress.Split(out lowDest, out highDest);
            writer.Write(lowDest);
            writer.Write(highDest);

            writer.Close();
        }
        public static ushort? LoadCMDFile(string FilePath, IMemory mem)
        {
            // Returns the address to start executing

            byte code;
            int length;
            byte[] data = new byte[0x101];

            ushort destAddress;
            ushort? execAddress = null;

            if (File.Exists(FilePath))
            {
                try
                {
                    if (LoadBinaryFile(FilePath, out byte[] b))
                    {
                        int i = 0;
                        while (i < b.Length)
                        {
                            code = b[i++];
                            length = b[i++];

                            if (length == 0)
                                length = 0x100;

                            Array.Copy(b, i, data, 0, Math.Min(length, b.Length - i));
                            i += length;

                            switch (code)
                            {
                                case 0x00:
                                    // do nothing
                                    break;
                                case 0x01:          // object code (load block)
                                    switch (length)
                                    {
                                        case 1:
                                            destAddress = Lib.CombineBytes(data[0], b[i++]);
                                            for (int k = 0; k < 0xFF; k++)
                                                mem[destAddress++] = b[i++];
                                            break;
                                        case 2:
                                            destAddress = Lib.CombineBytes(data[0], data[1]);
                                            for (int k = 0; k < 0x100; k++)
                                                mem[destAddress++] = b[i++];
                                            break;
                                        default:
                                            destAddress = Lib.CombineBytes(data[0], data[1]);
                                            for (int k = 2; k < length; k++)
                                                mem[destAddress++] = data[k];
                                            break;
                                    }
                                    Debug.Assert(length != 0x00);
                                    break;

                                case 0x02:          // transfer address
                                    if (length == 0x01)
                                        execAddress = data[0];
                                    else if (length == 0x02)
                                        execAddress = Lib.CombineBytes(data[0], data[1]);
                                    else
                                        throw new Exception("CMD file Error.");
                                    break;
                                case 0x03:
                                    // Do nothing (non executable marker)
                                    break;
                                case 0x04:          // end of partitioned data set member
                                                    // Do nothing
                                    break;
                                case 0x05:          // load module header
                                                    // Do nothing
                                    break;
                                case 0x06:          // partitioned data set header
                                                    // Do nothing
                                    break;
                                case 0x07:          // patch name header
                                                    // Do nothing
                                    break;
                                case 0x08:          // ISAM directory entry
                                                    // Do nothing
                                    break;
                                case 0x09:          // unused code
                                    break;
                                case 0x0A:          // end of ISAM directory
                                                    // Do nothing
                                    break;
                                case 0x0C:          // PDS directory entry
                                                    // Do nothing
                                    break;
                                case 0x0E:          // end of PDS directory
                                                    // Do nothing
                                    break;
                                case 0x10:          // yanked load block
                                                    // Do nothing
                                    break;
                                case 0x1F:          // copyright block
                                                    // Do nothing
                                    break;
                                default:
                                    //throw new Exception("Error in CMD file.");
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogException(ex);
                    return null;
                }
            }
            return execAddress;
        }
        public static string GetFloppyFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog, bool DskOnly)
        {
            string ext = DskOnly ? "dsk" : Path.GetExtension(DefaultPath);

            if (string.IsNullOrWhiteSpace(ext))
                ext = "dsk";

            return Dialogs.UserSelectFile(Save: Save,
                                          DefaultPath: DefaultPath,
                                          Title: Prompt,
                                          Filter: DskOnly ? "TRS-80 DSK Files (*.dsk)|*.dsk|All Files (*.*)|*.*"
                                                          : "TRS-80 DSK Files (*.dsk;*.dmk;*.jv1;*.jv3)|*.dsk;*.dmk;*.jv1;*.jv3|All Files (*.*)|*.*",
                                          DefaultExtension: ext,
                                          SelectFileInDialog: SelectFileInDialog);
        }

        // returns false if the user cancelled a needed save
        public static bool SaveFloppies(Computer Computer)
        {
            // returns true on user cancel
            for (byte b = 0; b < 4; b++)
            {
                if (!SaveFloppyIfRequired(Computer, b))
                    return false;
            }
            return true;
        }
        public static bool SaveFloppyIfRequired(Computer Computer, byte DriveNum)
        {
            bool? save = false;

            if (Computer.DiskHasChanged(DriveNum))
                save = Dialogs.AskYesNoCancel(string.Format("Drive {0} has changed. Save it?", DriveNum));

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.GetFloppyFilePath(DriveNum);
                if (string.IsNullOrWhiteSpace(path) || Floppy.IsFileNameToken(path))
                {
                    path = GetFloppyFilePath("Choose path to save floppy", Settings.DefaultFloppyDirectory, true, false, true);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return false;
                    }
                    else
                    {
                        Computer.SetFloppyFilePath(DriveNum, path);
                        SaveDefaultDriveFileName(DriveNum, path);
                    }
                }
                Computer.SaveFloppy(DriveNum);
            }
            return true;
        }
        public static bool SaveTapeIfRequired(Computer Computer)
        {
            bool? save = false;

            if (Computer.TapeChanged)
                save = Dialogs.AskYesNoCancel("The tape has been written to. Save it?");

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.TapeFilePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Dialogs.GetTapeFilePath(Settings.LastCasFile, true);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return false;
                    }
                    else
                    {
                        Computer.TapeFilePath = path;
                        Settings.LastCasFile = path;
                    }
                }
                Computer.TapeSave();
            }
            return true;
        }
        public static bool MakeFloppyFromFile(string FilePath)
        {
            if (LoadBinaryFile(FilePath, out byte[] bytes))
            {
                byte[] diskImage = DMK.MakeFloppyFromFile(bytes, Path.GetFileName(FilePath)).Serialize(ForceDMK: true);
                if (diskImage.Length > 0)
                    return SaveBinaryFile(Path.ChangeExtension(FilePath, "dsk"), diskImage);
            }
            return false;
        }
        private static string userPath = null;
        public static string DocsPath
        {
            get
            {
                if (userPath == null)
                {
                    userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sharp80");
                    Directory.CreateDirectory(userPath);
                }
                return userPath;
            }
        }
        private static string appDataPath = null;
        public static string AppDataPath
        {
            get
            {
                if (appDataPath == null)
                {
                    appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sharp80");
                    Directory.CreateDirectory(appDataPath);
                }
                return appDataPath;
            }
        }   
    }
}

