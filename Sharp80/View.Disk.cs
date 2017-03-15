/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Text;

namespace Sharp80
{
    internal class ViewDisk : View
    {
        protected override ViewMode Mode => ViewMode.Disk;
        protected override bool ForceRedraw => false;
        protected override bool CanSendKeysToEmulation => false;

        protected override void Activate()
        {
            DriveNumber = null;
        }
        protected override bool processKey(KeyState Key)
        {
            if (Key.Released)
            {
                // Some selections fire on key released to avoid keys leaking into 
                // Windows' dialogs.
                switch (Key.Key)
                {
                    case KeyCode.F:
                        if (Settings.DiskEnabled)
                            LoadFloppy(false);
                        break;
                    case KeyCode.L:
                        if (Settings.DiskEnabled)
                            LoadFloppy(true);
                        break;
                    default:
                        return base.processKey(Key);
                }
            }
            else
            {
                if (Settings.DiskEnabled)
                {
                    switch (Key.Key)
                    {
                        case KeyCode.D0:
                            DriveNumber = DriveNumber ?? 0;
                            break;
                        case KeyCode.D1:
                            DriveNumber = DriveNumber ?? 1;
                            break;
                        case KeyCode.D2:
                            DriveNumber = DriveNumber ?? 2;
                            break;
                        case KeyCode.D3:
                            DriveNumber = DriveNumber ?? 3;
                            break;
                        case KeyCode.B:
                            LoadFloppy(false, Storage.FILE_NAME_NEW);
                            break;
                        case KeyCode.D:
                            ToggleFloppyEnable();
                            break;
                        case KeyCode.E:
                            EjectFloppy();
                            break;
                        case KeyCode.T:
                            LoadFloppy(false, Storage.FILE_NAME_TRSDOS);
                            break;
                        case KeyCode.U:
                            LoadFloppy(false, Storage.FILE_NAME_UNFORMATTED);
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
                            return base.processKey(Key);
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
            Invalidate();
            return true;
        }
        protected override byte[] GetViewBytes()
        {
            string s = Header("Sharp 80 Floppy Disk Manager");

            if (Settings.DiskEnabled)
            {
                if (DriveNumber.HasValue)
                {
                    bool diskLoaded = !Computer.DriveIsUnloaded(DriveNumber.Value);
                    s += DrawDisk(DriveNumber.Value) +
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
                             Format("[L] Load floppy from included library") +
                             Format("[T] Load TRSDOS floppy") +
                             Format("[B] Insert blank formatted floppy") +
                             Format("[U] Insert unformatted floppy");
                    }
                    s += Format() +
                         Format() +
                         Format("[Escape] Back to all drives");
                }
                else
                {
                    for (byte i = 0; i < FloppyController.NUM_DRIVES; i++)
                    {
                        s += DrawDisk(i);
                        if (i < FloppyController.NUM_DRIVES - 1)
                            s += Format();
                    }
                    s += Separator() +
                         Format("Choose a floppy drive [0] to [3].") +
                         Format("[D] to disable drives and operate in Basic only.");
                }
            }
            else
            {
                s += Format() +
                     Format() +
                     Center("FLOPPY DRIVES DISABLED") +
                     Format() +
                     Format() +
                     Indent("[D] to enable drives.");
            }
            return PadScreen(Encoding.ASCII.GetBytes(s));
        }
        private static string DrawDisk(byte DiskNum)
        {
            var d = Computer.GetFloppy(DiskNum);

            string line1;
            if (d == null)
                line1 = string.Format("Drive #{0}: Unloaded", DiskNum);
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
                line2 = FitFilePath(d.FilePath, ScreenMetrics.NUM_SCREEN_CHARS_X);

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
                WriteToByteArray(cells, 0x006, string.Format("Drive {0} is empty.", DriveNum));
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
        private void LoadFloppy(bool FromLibrary, string Path = null)
        {
            if (DriveNumber.HasValue)
            {
                string path = Path ?? (FromLibrary ? System.IO.Path.Combine(Storage.AppDataPath, @"Disks\")
                                                   : Storage.GetDefaultDriveFileName(DriveNumber.Value));

                if (!Storage.IsFileNameToken(path))
                {
                    bool selectFile = true;
                    
                    path = Storage.GetFloppyFilePath(Prompt: string.Format("Select floppy file to load in drive {0}", DriveNumber.Value),
                                             DefaultPath: path,
                                             Save: false,
                                             SelectFileInDialog: selectFile,
                                             DskOnly: false);
                }
                if (path.Length > 0 && (Storage.IsFileNameToken(path) || File.Exists(path)))
                {
                    if (Storage.SaveFloppyIfRequired(Computer, DriveNumber.Value))
                    {
                        switch (path)
                        {
                            case Storage.FILE_NAME_NEW:
                                Computer.LoadFloppy(DriveNumber.Value, Storage.MakeBlankFloppy(Formatted: true));
                                break;
                            case Storage.FILE_NAME_UNFORMATTED:
                                Computer.LoadFloppy(DriveNumber.Value, Storage.MakeBlankFloppy(Formatted: false));
                                break;
                            case Storage.FILE_NAME_TRSDOS:
                                Computer.LoadTrsDosFloppy(DriveNumber.Value);
                                break;
                            default:
                                Computer.LoadFloppy(DriveNumber.Value, path);
                                Settings.DefaultFloppyDirectory = System.IO.Path.GetDirectoryName(path);
                                break;
                        }

                        Storage.SaveDefaultDriveFileName(DriveNumber.Value, path);
                        
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
            bool restart = false;
            if (Computer.HasRunYet)
            {
                string caption = (Settings.DiskEnabled ? "Disabling" : "Enabling") + " the floppy controller requires a restart. Continue?";
                if (!Dialogs.AskYesNo(caption))
                    return;
                if (Computer.IsRunning)
                    restart = true;
                Settings.DiskEnabled = !Settings.DiskEnabled;
                InvokeUserCommand(UserCommand.HardReset);
            }
            else
            {
                Settings.DiskEnabled = !Settings.DiskEnabled;
            }
            MessageCallback("Floppy Controller " + (Settings.DiskEnabled ? "Enabled" : "Disabled"));
            if (restart)
            {
                Computer.Start();
                RevertMode();
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
    }
}
