/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace Sharp80
{
    public static class Storage
    {
        public const string FILE_NAME_TRSDOS = "{TRSDOS}";
        public const string FILE_NAME_NEW = "{NEW}";
        public const string FILE_NAME_UNFORMATTED = "{UNFORMATTED}";

        private static string userPath = null;
        private static string appDataPath = null;
        private static string libraryPath = null;

        // SYSTEM PATHS

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

        // DISKS

        internal static string GetDefaultDriveFileName(byte DriveNum)
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
            if (File.Exists(fileName) || IsFileNameToken(fileName))
                return fileName;
            else
                return String.Empty;
        }
        /// <summary>
        /// Should be called when the floppy is put in the drive. Note that the
        /// floppy's file path may be empty but we may want to save a token
        /// value like {NEW}
        /// </summary>
        internal static void SaveDefaultDriveFileName(byte DriveNum, string FilePath)
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
        internal static string GetFloppyFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog, bool DskOnly)
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
        internal static bool SaveFloppies(Computer Computer)
        {
            // returns true on user cancel
            for (byte b = 0; b < 4; b++)
            {
                if (!SaveFloppyIfRequired(Computer, b))
                    return false;
            }
            return true;
        }
        internal static bool SaveFloppyIfRequired(Computer Computer, byte DriveNum)
        {
            bool? save = false;

            if (Computer.DiskHasChanged(DriveNum))
                save = Dialogs.AskYesNoCancel($"Drive {DriveNum} has changed. Save it?");

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.GetFloppyFilePath(DriveNum);
                if (!IsPathWritable(path))
                {
                    if (IsLibraryFile(path))
                        path = GetFloppyFilePath("Choose path to save floppy", Path.Combine(Settings.DefaultFloppyDirectory, Path.GetFileName(path)), true, true, true);
                    else
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

        // TAPES

        /// <summary>
        /// Returns a token or the latest path used. The path is confirmed to exist.
        /// </summary>
        /// <returns></returns>
        internal static string GetDefaultTapeFileName()
        {
            string path = Settings.LastTapeFile;
            if (File.Exists(path) || IsFileNameToken(path))
                return path;
            else
                return String.Empty;
        }
        internal static string GetTapeFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog)
        {
            return Dialogs.UserSelectFile(Save: Save,
                                          DefaultPath: DefaultPath,
                                          Title: Prompt,
                                          Filter: "TRS-80 Tape Files (*.cas)|*.cas|All Files (*.*)|*.*",
                                          DefaultExtension: ".cas",
                                          SelectFileInDialog: SelectFileInDialog);
        }
        internal static bool SaveTapeIfRequired(Computer Computer)
        {
            bool? save = false;

            if (Computer.TapeChanged)
                save = Dialogs.AskYesNoCancel("The tape has been written to. Save it?");

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.TapeFilePath;
                if (String.IsNullOrWhiteSpace(path) || IsFileNameToken(path) || !Directory.Exists(Path.GetDirectoryName(path)))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(path)))
                        path = Path.Combine(AppDataPath, "Tapes\\");

                    path = Dialogs.GetTapeFilePath(path, true);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return false;
                    }
                    else
                    {
                        Computer.TapeFilePath = path;
                        Settings.LastTapeFile = path;
                    }
                }
                Computer.TapeSave();
            }
            return true;
        }

        // ASSEMBLY

        internal static bool GetAsmFilePath(out string Path)
        {
            Path = Settings.LastAsmFile;
            Path = Dialogs.GetAssemblyFile(Path, false);

            if (Path.Length > 0)
            {
                Settings.LastAsmFile = Path;
                return true;
            }
            else
            {
                return false;
            }
        }

        // SNAPSHOTS

        internal static string DefaultSnapshotDir => Path.Combine(AppDataPath, @"Snapshots\");

        // PRINTER

        internal static string DefaultPrintDir => Path.Combine(AppDataPath, @"Print\");

        // MISC

        internal static bool IsFileNameToken(string Path) => Path == FILE_NAME_UNFORMATTED || Path == FILE_NAME_NEW || Path == FILE_NAME_TRSDOS;
        internal static bool IsPathWritable(string Path) => !string.IsNullOrWhiteSpace(Path) && !IsFileNameToken(Path) && !IsLibraryFile(Path);
        internal static string LibraryPath
        {
            get
            {
                if (libraryPath is null)
                {
                    libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Library");
                    if (!Directory.Exists(libraryPath))
                    {
                        libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Sharp80\Library");
                        if (!Directory.Exists(libraryPath))
                        {
                            libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Sharp80\Library");
                            if (!Directory.Exists(libraryPath))
                                libraryPath = String.Empty;
                        }
                    }
                }
                return libraryPath;
            }
        }
        internal static bool IsLibraryFile(string Path) => LibraryOK && Path.StartsWith(LibraryPath, StringComparison.OrdinalIgnoreCase);
        internal static bool LibraryOK => !String.IsNullOrWhiteSpace(LibraryPath);
        /// <summary>
        /// Returns false if the user cancelled a needed save
        /// </summary>
        internal static bool SaveChangedStorage(Computer Computer) => SaveFloppies(Computer) && SaveTapeIfRequired(Computer);
    }
}

