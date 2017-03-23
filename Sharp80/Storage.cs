/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace Sharp80
{
    internal static class Storage
    {
        public const string FILE_NAME_TRSDOS =      "{TRSDOS}";
        public const string FILE_NAME_NEW =         "{NEW}";
        public const string FILE_NAME_UNFORMATTED = "{UNFORMATTED}";

        private static string userPath = null;
        private static string appDataPath = null;

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
        public static bool LoadBinaryFile(string FilePath, out byte[] Bytes)
        {
            try
            {
                Bytes = File.ReadAllBytes(FilePath);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" already is use.");
                else if (ex is FileNotFoundException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" not found.");
                else
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser);

                Bytes = null;
                return false;
            }
        }
        public static bool LoadTextFile(string FilePath, out string Text)
        {
            try
            {
                Text = File.ReadAllText(FilePath);
                return true;
            }
            catch
            {
                Text = null;
                return false;
            }
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
                ex.Data["ExtraMessage"] = $"Exception saving file {FilePath}";

                if (ex is IOException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" already is use.");
                else if (ex is FileNotFoundException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" not found.");
                else
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser);

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
            if (File.Exists(fileName) || IsFileNameToken(fileName))
                return fileName;
            else
                return String.Empty;
        }

        /// <summary>
        /// Returns a token or the latest path used. The path is confirmed to exist.
        /// </summary>
        /// <returns></returns>
        public static string GetDefaultTapeFileName()
        {
            string path = Settings.LastTapeFile;
            if (File.Exists(path) || IsFileNameToken(path))
                return path;
            else
                return String.Empty;
        }
        public static string GetTapeFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog)
        {
            return Dialogs.UserSelectFile(Save: Save,
                                          DefaultPath: DefaultPath,
                                          Title: Prompt,
                                          Filter: "TRS-80 Tape Files (*.cas)|*.cas|All Files (*.*)|*.*",
                                          DefaultExtension: ".cas",
                                          SelectFileInDialog: SelectFileInDialog);
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
            f.FilePath = Formatted ? FILE_NAME_NEW : FILE_NAME_UNFORMATTED;

            return f;
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
        public static bool GetAsmFilePath(out string Path)
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
        public static bool IsFileNameToken(string Path)
        {
            return Path == FILE_NAME_UNFORMATTED || Path == FILE_NAME_NEW || Path == FILE_NAME_TRSDOS;
        }
        private static string libraryPath = null;
        public static string LibraryPath
        {
            get
            {
                libraryPath = libraryPath ?? Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), @"Library");
                return libraryPath;
            }
        }
        public static bool IsLibraryFile(string Path) => Path.StartsWith(LibraryPath);

        /// returns false if the user cancelled a needed save
        public static bool SaveChangedStorage(Computer Computer)
        {
            return SaveFloppies(Computer) && SaveTapeIfRequired(Computer);
        }
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
                save = Dialogs.AskYesNoCancel($"Drive {DriveNum} has changed. Save it?");

            if (!save.HasValue)
                return false;

            if (save.Value)
            {
                var path = Computer.GetFloppyFilePath(DriveNum);
                if (string.IsNullOrWhiteSpace(path) || IsFileNameToken(path) || IsLibraryFile(path))
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
                if (String.IsNullOrWhiteSpace(path) || IsFileNameToken(path)|| !Directory.Exists(Path.GetDirectoryName(path)))
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
        public static bool MakeFloppyFromFile(string FilePath, out string NewPath)
        {
            if (LoadBinaryFile(FilePath, out byte[] bytes))
            {
                byte[] diskImage = DMK.MakeFloppyFromFile(bytes, Path.GetFileName(FilePath)).Serialize(ForceDMK: true);
                if (diskImage.Length > 0)
                    return SaveBinaryFile(NewPath = FilePath.ReplaceExtension("dsk"), diskImage);
            }
            NewPath = String.Empty;
            return false;
        }
    }
}

