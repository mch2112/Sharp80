/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace Sharp80.TRS80
{
    public static class Storage
    {
        public const string FILE_NAME_TRSDOS = "{TRSDOS}";
        public const string FILE_NAME_NEW = "{NEW}";
        public const string FILE_NAME_UNFORMATTED = "{UNFORMATTED}";

        private static ISettings settings;
        private static IDialogs dialogs;
        private static string userPath = null;
        private static string appDataPath = null;
        private static string libraryPath = null;

        public static void Initialize(ISettings Settings, IDialogs Dialogs)
        {
            settings = Settings;
            dialogs = Dialogs;
        }

        // SYSTEM PATHS

        public static string DocsPath
        {
            get
            {
                if (userPath is null)
                {
                    userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sharp80");
                    if (!Directory.Exists(userPath))
                        Directory.CreateDirectory(userPath);
                }
                return userPath;
            }
        }
        public static string AppDataPath
        {
            get
            {
                if (appDataPath is null)
                {
                    appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sharp80");
                    if (!Directory.Exists(appDataPath))
                        Directory.CreateDirectory(appDataPath);
                }
                return appDataPath;
            }
        }
        public static string DefaultSnapshotDir => Path.Combine(AppDataPath, @"Snapshots\");
        public static string DefaultPrintDir => Path.Combine(AppDataPath, @"Print\");

        // DISKS

        public static string GetDefaultDriveFileName(byte DriveNum)
        {
            string fileName = String.Empty;

            switch (DriveNum)
            {
                case 0:
                    fileName = settings.Disk0Filename;
                    break;
                case 1:
                    fileName = settings.Disk1Filename;
                    break;
                case 2:
                    fileName = settings.Disk2Filename;
                    break;
                case 3:
                    fileName = settings.Disk3Filename;
                    break;
            }
            if (File.Exists(fileName) || IsFileNameToken(fileName))
                return fileName;
            else if (String.IsNullOrWhiteSpace(fileName))
                return Path.Combine(AppDataPath, @"Disks\");
            else
                return String.Empty;
        }
        /// <summary>
        /// Should be called when the floppy is put in the drive. Note that the
        /// floppy's file path may be empty but we may want to save a token
        /// value like {NEW}
        /// </summary>
        public static void SaveDefaultDriveFileName(byte DriveNum, string FilePath)
        {
            switch (DriveNum)
            {
                case 0:
                    settings.Disk0Filename = FilePath;
                    break;
                case 1:
                    settings.Disk1Filename = FilePath;
                    break;
                case 2:
                    settings.Disk2Filename = FilePath;
                    break;
                case 3:
                    settings.Disk3Filename = FilePath;
                    break;
            }
        }
        public static string GetFloppyFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog, bool DskOnly)
        {
            string ext = DskOnly ? "dsk" : Path.GetExtension(DefaultPath);

            if (string.IsNullOrWhiteSpace(ext))
                ext = "dsk";

            return dialogs.UserSelectFile(Save: Save,
                                          DefaultPath: DefaultPath,
                                          Title: Prompt,
                                          Filter: DskOnly ? "TRS-80 DSK Files (*.dsk)|*.dsk|All Files (*.*)|*.*"
                                                          : "TRS-80 DSK Files (*.dsk;*.dmk;*.jv1;*.jv3)|*.dsk;*.dmk;*.jv1;*.jv3|All Files (*.*)|*.*",
                                          DefaultExtension: ext,
                                          SelectFileInDialog: SelectFileInDialog);
        }
        public static bool SaveFloppies(Computer Computer)
        {
            // returns false on user cancel
            for (byte b = 0; b < 4; b++)
            {
                if (!SaveFloppyIfRequired(Computer, b))
                    return false;
            }
            return true;
        }
        public static bool SaveFloppyIfRequired(TRS80.Computer Computer, byte DriveNum)
        {
            bool? save = false;

            if (Computer.DiskHasChanged(DriveNum))
                save = dialogs.AskYesNoCancel($"Drive {DriveNum} has changed. Save it?");

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.GetFloppyFilePath(DriveNum);
                if (!IsPathWritable(path))
                {
                    var defaultPath = settings.DefaultFloppyDirectory;
                    if (String.IsNullOrWhiteSpace(defaultPath) || !Directory.Exists(defaultPath))
                        path = DocsPath;

                    if (IsLibraryFile(path))
                        path = GetFloppyFilePath("Choose path to save floppy", Path.Combine(defaultPath, Path.GetFileName(path)), true, true, true);
                    else
                        path = GetFloppyFilePath("Choose path to save floppy", defaultPath, true, false, true);

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
        public static string GetDefaultTapeFileName()
        {
            string path = settings.LastTapeFile;
                
            if (File.Exists(path) || IsFileNameToken(path))
                return path;
            else if (String.IsNullOrWhiteSpace(path))
                return Path.Combine(AppDataPath, @"Tapes\");
            else
                return String.Empty;
        }
        internal static string LastTapeFile => settings.LastTapeFile;
        public static string GetTapeFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog)
        {
            return dialogs.UserSelectFile(Save: Save,
                                          DefaultPath: DefaultPath,
                                          Title: Prompt,
                                          Filter: "TRS-80 Tape Files (*.cas)|*.cas|All Files (*.*)|*.*",
                                          DefaultExtension: ".cas",
                                          SelectFileInDialog: SelectFileInDialog);
        }
        public static bool SaveTapeIfRequired(Computer Computer)
        {
            bool? save = false;

            if (Computer.TapeChanged)
                save = dialogs.AskYesNoCancel("The tape has been written to. Save it?");

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.TapeFilePath;
                if (String.IsNullOrWhiteSpace(path) || IsFileNameToken(path) || !Directory.Exists(Path.GetDirectoryName(path)))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(path)))
                        path = Path.Combine(AppDataPath, "Tapes\\");

                    path = dialogs.GetTapeFilePath(path, true);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return false;
                    }
                    else
                    {
                        Computer.TapeFilePath = path;
                        settings.LastTapeFile = path;
                    }
                }
                Computer.TapeSave();
            }
            return true;
        }

        // ASSEMBLY

        public static bool GetAsmFilePath(out string Path)
        {
            Path = settings.LastAsmFile;

            if (String.IsNullOrWhiteSpace(Path))
                Path = System.IO.Path.Combine(AppDataPath, @"ASM Files\");

            Path = dialogs.GetAssemblyFile(Path, false);

            if (Path.Length > 0)
            {
                settings.LastAsmFile = Path;
                return true;
            }
            else
            {
                return false;
            }
        }

        // MISC

        public static string LibraryPath
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
        public static bool IsFileNameToken(string Path) => Path == FILE_NAME_UNFORMATTED || Path == FILE_NAME_NEW || Path == FILE_NAME_TRSDOS;
        public static bool IsPathWritable(string Path) => !string.IsNullOrWhiteSpace(Path) && !IsFileNameToken(Path) && !IsLibraryFile(Path);
        public static bool IsLibraryFile(string Path) => LibraryOK && Path.StartsWith(LibraryPath, StringComparison.OrdinalIgnoreCase);
        public static bool LibraryOK => !String.IsNullOrWhiteSpace(LibraryPath);
    }
}

