/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharp80
{
    internal class ViewDisk : View
    {
        protected override ViewMode Mode => ViewMode.Disk;
        protected override bool ForceRedraw => false;
        protected override bool CanSendKeysToEmulation => false;

        private List<(string Name, string Path, bool IsDir)> libraryMenu = null;
        private List<(string Name, string Path, bool IsDir)> LibraryMenu => libraryMenu ?? (libraryMenu = GetLibraryMenu());
        private static DirectoryInfo BaseLibraryDir { get; set; }
        private DirectoryInfo LibraryDir { get; set; }

        private string header = Header($"{ProductInfo.PRODUCT_NAME} Floppy Disk Manager");

        static ViewDisk()
        {
            BaseLibraryDir = new DirectoryInfo(Path.Combine(Storage.LibraryPath, @"Disks"));
        }

        protected override void Activate()
        {
            DriveNumber = null;
            LibraryDir = null;
        }

        protected override bool processKey(KeyState Key)
        {
            Invalidate();
            if (Key.IsUnmodified)
            {
                if (Key.Released)
                {
                    // Some selections fire on key released to avoid keys leaking into 
                    // Windows' dialogs.
                    switch (Key.Key)
                    {
                        case KeyCode.F:
                            if (Computer.DiskUserEnabled)
                                LoadFloppy();
                            break;
                        case KeyCode.L:
                            if (Storage.LibraryOK)
                                if (DriveNumber.HasValue && LibraryDir is null)
                                    LibraryDir = BaseLibraryDir;
                            break;
                        default:
                            return base.processKey(Key);
                    }
                }
                else
                {
                    if (Computer.DiskUserEnabled)
                    {
                        switch (Key.Key)
                        {
                            case KeyCode.B:
                                LoadFloppy(Storage.FILE_NAME_NEW);
                                break;
                            case KeyCode.D:
                                ToggleFloppyEnable();
                                break;
                            case KeyCode.E:
                                EjectFloppy();
                                break;
                            case KeyCode.T:
                                LoadFloppy(Storage.FILE_NAME_TRSDOS);
                                break;
                            case KeyCode.U:
                                LoadFloppy(Storage.FILE_NAME_UNFORMATTED);
                                break;
                            case KeyCode.W:
                                ToggleWriteProtection();
                                break;
                            case KeyCode.Z:
                                if (DriveNumber.HasValue)
                                    CurrentMode = ViewMode.Zap;
                                break;
                            case KeyCode.Return:
                                if (DriveNumber.HasValue)
                                    DriveNumber = null;
                                else
                                    return base.processKey(Key);
                                break;
                            case KeyCode.Escape:
                                if (!EscapeLibrary())
                                    if (DriveNumber.HasValue)
                                        DriveNumber = null;
                                    else
                                        RevertMode();
                                break;
                            case KeyCode.F8:
                                if (!Computer.IsRunning)
                                    CurrentMode = ViewMode.Normal;
                                return base.processKey(Key);
                            default:
                                if (Key.TryGetNum(out byte i))
                                {
                                    if (!SelectLibrary(i))
                                        if (i <= 4)
                                            DriveNumber = i;
                                    return true;
                                }
                                else
                                {
                                    return base.processKey(Key);
                                }
                        }
                    }
                    else
                    {
                        switch (Key.Key)
                        {
                            case KeyCode.D:
                                ToggleFloppyEnable();
                                break;
                            case KeyCode.Escape:
                                RevertMode();
                                break;
                            case KeyCode.F8:
                                CurrentMode = ViewMode.Normal;
                                return base.processKey(Key);
                            default:
                                return base.processKey(Key);
                        }
                    }
                }
            }
            else
            {
                return base.processKey(Key);
            }
            return true;
        }
        protected override byte[] GetViewBytes()
        {
            string ret = header;

            if (Computer.DiskUserEnabled)
            {
                if (DriveNumber.HasValue)
                {
                    if (LibraryDir is null)
                        ret += GetViewForDisk();
                    else
                        ret += GetViewForLibrary();
                }
                else
                {
                    ret += GetView();
                }
            }
            else
            {
                ret = Format() +
                      Format() +
                      Center("FLOPPY DRIVES DISABLED") +
                      Format() +
                      Format() +
                      Indent("[D] to enable drives.");
            }
            return PadScreen(Encoding.ASCII.GetBytes(ret));
        }

        private string GetView()
        {
            string s = String.Empty;
            for (byte i = 0; i < FloppyController.NUM_DRIVES; i++)
            {
                s += DrawDisk(i);
                if (i < FloppyController.NUM_DRIVES - 1)
                    s += Format();
            }
            s += Separator() +
                 Format("Choose a floppy drive [0] to [3].") +
                 Format("[D] to disable drives and operate in Basic only.");
            return s;
        }

        private string GetViewForLibrary()
        {
            if (Storage.LibraryOK)
            {
                bool isTop = LibraryDir.FullName == BaseLibraryDir.FullName;

                if (LibraryDir is null)
                {
                    LibraryDir = BaseLibraryDir;
                    libraryMenu = null;
                }

                string ret = Format(isTop ? String.Empty : LibraryDir.Name) +
                             Format();

                for (int i = 0; i < LibraryMenu.Count; i++)
                    ret += Format($"[{(i + 1) % 10}] {LibraryMenu[i].Name}");

                ret += Format().Repeat(11 - LibraryMenu.Count);

                if (isTop)
                    ret += "[Esc] to exit library.";
                else
                    ret += "[Esc] to go back.";

                return ret;
            }
            else
            {
                return "Library Error.";
            }
        }
        private List<(string Name, string Path, bool IsDir)> GetLibraryMenu()
        {
            var ld = new List<(string Name, string Path, bool IsDir)>();

            if (Storage.LibraryOK)
            {
                foreach (var d in LibraryDir.GetDirectories())
                    ld.Add((d.Name, d.FullName, true));

                foreach (var d in LibraryDir.GetFiles())
                    ld.Add((Path.GetFileNameWithoutExtension(d.Name), d.FullName, false));

                System.Diagnostics.Debug.Assert(ld.Count <= 10);
            }
            return ld;
        }
        private static string GetViewForDisk()
        {
            string s;

            bool diskLoaded = !Computer.DriveIsUnloaded(DriveNumber.Value);

            s = DrawDisk(DriveNumber.Value) +
                    Format() +
                    Separator('-') +
                    Format() +
                    Format();

            if (diskLoaded)
            {
                s += Format("[E] Eject floppy") +
                     Format(string.Format("[W] Toggle write protection {0}", (Computer.GetFloppy(DriveNumber.Value)?.WriteProtected ?? false) ? "[ON] /  OFF " : " ON  / [OFF]")) +
                     Format("[Z] Disk zap (view sectors)") +
                     Format() +
                     Format();
            }
            else
            {
                s += Format("[F] Load floppy from file") +
                     (Storage.LibraryOK ? Format("[L] Load floppy from included library") : String.Empty) +
                     Format("[T] Load TRSDOS floppy") +
                     Format("[B] Insert blank formatted floppy") +
                     Format("[U] Insert unformatted floppy");
            }
            s += Format() +
                 Format() +
                 Format("[Esc] Back to all drives");
            return s;
        }

        private static string DrawDisk(byte DiskNum)
        {
            var d = Computer.GetFloppy(DiskNum);

            string line1;
            if (d == null)
                line1 = $"Drive #{DiskNum}: Unloaded";
            else
                line1 = string.Format("Drive #{0}: {1}  {2} Tks  {3} {4}",
                                      DiskNum,
                                      ((d.DoubleSided) ? "Dbl Sided" : "Sgl Sided"),
                                      d.NumTracks,
                                      d.WriteProtected ? "[WP]" : string.Empty,
                                      d.Formatted ? string.Empty : "UNFORMATTED");

            string line2;
            if (d == null)
                line2 = String.Empty;
            else
                line2 = FitFilePath(d.FileDisplayName, ScreenMetrics.NUM_SCREEN_CHARS_X);

            return Format(line1) + Format(line2);
        }
        public static byte[] GetDiskZapText(byte DriveNum,
                                            byte TrackNum,
                                            bool SideOne,
                                            bool DoubleSided,
                                            SectorDescriptor sd,
                                            bool IsEmpty
                                            )
        {
            int numBytes = Math.Min(0x100, sd?.SectorData?.Length ?? 0);

            byte[] cells = new byte[ScreenMetrics.NUM_SCREEN_CHARS];

            WriteToByteArray(cells, 0x000, "Dsk");
            cells[0x040] = DriveNum.ToHexCharByte();

            WriteToByteArray(cells, 0x0C0, "Trk");
            WriteToByteArrayHex(cells, 0x100, TrackNum);

            if (DoubleSided)
            {
                WriteToByteArray(cells, 0x280, "Side");
                cells[0x300] = (byte)(SideOne ? '1' : '0');
            }

            if (sd != null)
            {
                WriteToByteArray(cells, 0x180, "Sec");
                WriteToByteArrayHex(cells, 0x1C0, sd.SectorNumber);

                WriteToByteArray(cells, 0x200, sd.DoubleDensity ? "DD" : "SD");

                if (sd.TrackNumber != TrackNum)
                    WriteToByteArrayHex(cells, 0x140, sd.TrackNumber);

                if (!IsEmpty)
                {
                    switch (sd.DAM)
                    {
                        case Floppy.DAM_NORMAL:
                            WriteToByteArray(cells, 0x300, "Std");
                            break;
                        case Floppy.DAM_DELETED:
                            WriteToByteArray(cells, 0x300, "Del");
                            break;
                    }
                    if (sd.CrcError)
                        WriteToByteArray(cells, 0x380, "CRC");
                }
            }

            if (IsEmpty)
            {
                WriteToByteArray(cells, 0x006, $"Drive {DriveNum} is empty.");
            }
            else if (sd == null || numBytes == 0)
            {
                WriteToByteArray(cells, 0x006, "Sector is empty.");
            }
            else
            {
                int cell = 0;
                int rawCell = 0x30;

                for (int k = 0; k < 0x100; k++)
                {
                    if ((k & 0x0F) == 0x00)
                    {
                        // new line
                        cell += 0x05;
                        WriteToByteArrayHex(cells, cell, (byte)k);
                        cell += 2;
                    }
                    if (k < numBytes)
                    {
                        if (k % 2 == 0)
                            cell++;

                        byte b = sd.SectorData[k];

                        WriteToByteArrayHex(cells, cell, b);
                        cell += 2;

                        cells[rawCell++] = b;

                        if ((k & 0x0F) == 0x0F)
                        {
                            // wrap to new line on screen
                            rawCell += 0x30;
                            cell += 0x20 - 15;
                        }
                    }
                    else if ((k & 0x0F) == 0x00)
                    {
                        cell = k / 0x10 * ScreenMetrics.NUM_SCREEN_CHARS_X;
                    }
                }
            }
            return cells;
        }
        private void LoadFloppy()
        {
            if (DriveNumber.HasValue)
            {
                string path = Storage.GetDefaultDriveFileName(DriveNumber.Value);

                if (!Storage.IsFileNameToken(path))
                {
                    bool selectFile = true;
                    
                    path = Storage.GetFloppyFilePath(Prompt: $"Select floppy file to load in drive {DriveNumber.Value}",
                                             DefaultPath: path,
                                             Save: false,
                                             SelectFileInDialog: selectFile,
                                             DskOnly: false);
                }
                if (path.Length > 0 && (Storage.IsFileNameToken(path) || File.Exists(path)))
                    LoadFloppy(path);
            }
        }
        private void LoadFloppy(string Path)
        {
            if (DriveNumber.HasValue && Storage.SaveFloppyIfRequired(Computer, DriveNumber.Value))
            {
                switch (Path)
                {
                    case Storage.FILE_NAME_NEW:
                        Computer.LoadFloppy(DriveNumber.Value, new DMK(Formatted: true));
                        break;
                    case Storage.FILE_NAME_UNFORMATTED:
                        Computer.LoadFloppy(DriveNumber.Value, new DMK(Formatted: false));
                        break;
                    case Storage.FILE_NAME_TRSDOS:
                        Computer.LoadTrsDosFloppy(DriveNumber.Value);
                        break;
                    default:
                        if (Computer.LoadFloppy(DriveNumber.Value, Path))
                            if (!Storage.IsLibraryFile(Path))
                                Settings.DefaultFloppyDirectory = System.IO.Path.GetDirectoryName(Path);
                        break;
                }

                Storage.SaveDefaultDriveFileName(DriveNumber.Value, Path);

                if (DriveNumber == 0 && !Computer.DiskEnabled && Computer.HasRunYet)
                {
                    if (Dialogs.AskYesNo("You have inserted a disk but the computer is in tape only mode. Hard reset to disk mode?"))
                    {
                        bool running = Computer.IsRunning;
                        if (running)
                            Computer.Stop(true);
                        InvokeUserCommand(UserCommand.HardReset);
                        if (running)
                            Computer.Start();
                    }
                }
                Invalidate();
            }
        }
        private void EjectFloppy()
        {
            if (DriveNumber.HasValue)
            {
                if (Storage.SaveFloppyIfRequired(Computer, DriveNumber.Value))
                {
                    Computer.EjectFloppy(DriveNumber.Value);
                    Invalidate();
                }
            }
        }
        private void ToggleFloppyEnable()
        {
            if (!Computer.DiskUserEnabled || Storage.SaveFloppies(Computer))
            {
                bool restart = false;

                if (Computer.HasRunYet)
                {
                    string caption = (Computer.DiskUserEnabled ? "Disabling" : "Enabling") + " the floppy controller requires a restart. Continue?";
                    if (!Dialogs.AskYesNo(caption))
                        return;
                    if (Computer.IsRunning)
                        restart = true;
                    Computer.DiskUserEnabled = Settings.DiskEnabled = !Settings.DiskEnabled;
                    InvokeUserCommand(UserCommand.HardReset);
                }
                else
                {
                    Computer.DiskUserEnabled = Settings.DiskEnabled = !Settings.DiskEnabled;
                }
                MessageCallback("Floppy Controller " + (Computer.DiskUserEnabled ? "Enabled" : "Disabled"));
                if (restart)
                {
                    Computer.Start();
                    RevertMode();
                }
            }
        }
        private void ToggleWriteProtection()
        {
            if (DriveNumber.HasValue)
            {
                var f = Computer.GetFloppy(DriveNumber.Value);
                if (f != null)
                    f.WriteProtected = !f.WriteProtected;
            }
        }
        private bool SelectLibrary(int Index)
        {
            if (LibraryDir is null || !Storage.LibraryOK)
                return false;

            var item = LibraryMenu[(Index + 9) % 10];
            if (item.IsDir)
            {
                LibraryDir = new DirectoryInfo(item.Path);
            }
            else
            {
                LoadFloppy(item.Path);
                LibraryDir = null;
            }
            libraryMenu = null;
            Invalidate();
            return true;
        }
        private bool EscapeLibrary()
        {
            if (LibraryDir is null)
                return false;

            if (LibraryDir.FullName == BaseLibraryDir.FullName)
                LibraryDir = null;
            else
                LibraryDir = LibraryDir.Parent;
            libraryMenu = null;
            Invalidate();
            return true;
        }
    }
}
