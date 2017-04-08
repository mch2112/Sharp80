/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Linq;
using System.Text;

using Sharp80.TRS80;

namespace Sharp80.Views
{
    internal class ViewCmdFile : View
    {
        protected override ViewMode Mode => ViewMode.CmdFile;
        protected override bool ForceRedraw => false;
        protected override bool CanSendKeysToEmulation => false;

        protected override void Activate()
        {
            if ((!CmdFile?.IsLoaded) ?? false)
            {
                Load();
            }
            base.Activate();
        }

        private string runWarning = Format("Note: Some CMD files need the TRS-80 to be started first.") +
                                    Separator() +
                                    Format();

        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed)
            {
                Invalidate();
                if (Key.IsUnmodified)
                {
                    switch (Key.Key)
                    {
                        case KeyCode.A:
                            if (CmdFile is null)
                                InvokeAssembler(false);
                            return true;
                        case KeyCode.C:
                            Clear();
                            return true;
                        case KeyCode.D:
                            Disassemble();
                            return true;
                        case KeyCode.F:
                            if (CmdFile?.Valid ?? false)
                                MakeFloppyFromFile(out string _, CmdFile.FilePath);
                            return true;
                        case KeyCode.L:
                            Load();
                            Invalidate();
                            return true;
                        case KeyCode.R:
                            Run();
                            Invalidate();
                            return true;
                        case KeyCode.F8:
                            if (!Computer.HasRunYet)
                                CurrentMode = ViewMode.Normal;
                            return base.processKey(Key);
                    }
                }
                else if (Key.Alt)
                {
                    switch (Key.Key)
                    {
                        case KeyCode.Y:
                            var ret = base.processKey(Key);
                            Activate(); // since we're already here it won't happen.
                            return ret;
                    }
                }
            }
            return base.processKey(Key);
        }

        protected override byte[] GetViewBytes()
        {
            string fileInfo;
            string options;
            string warning = Format();

            if (CmdFile is null)
            {
                fileInfo = Format() +
                           Indent("No CMD File Loaded.") +
                           Format();

                options = Format("[R] Run a CMD file") +
                          Format() +
                          Format("[L] Load a CMD file without running") +
                          Format() +
                          Format("[A] Create and load CMD file with the Z80 Assembler");

                if (!Computer.HasRunYet)
                    warning = runWarning;
            }
            else if (!CmdFile.Valid)
            {
                fileInfo = Format("CMD File: " + FitFilePath(CmdFile.FilePath, ScreenMetrics.NUM_SCREEN_CHARS_X - "CMD File: ".Length)) +
                           Format() +
                           Format("Invalid CMD File");

                options = Format("[C] Clear this CMD File");
            }
            else
            {
                fileInfo = Format(FitFilePath(CmdFile.FilePath, ScreenMetrics.NUM_SCREEN_CHARS_X)) +
                           Format(string.Format("{0} bytes in {1} block{2} spanning {3}:{4}",
                                                CmdFile.Size,
                                                CmdFile.NumSegments,
                                                CmdFile.NumSegments == 1 ? String.Empty : "s",
                                                CmdFile.LowAddress.ToHexString(),
                                                CmdFile.HighAddress.ToHexString())) +
                           Format($"Title: {CmdFile.Title.Truncate(20)}  " + (CmdFile.ExecAddress.HasValue ? ("Execution Address: " + CmdFile.ExecAddress.Value.ToHexString()) : "NO EXECUTION ADDRESS!"));

                if (!Computer.HasRunYet)
                    warning = runWarning;

                options = Format("[R] Run CMD file") +
                          Format("[L] Reload CMD file") +
                          Format() +
                          Format("[C] Clear this CMD file") +
                          Format() +
                          Format("[F] Create a floppy and write this CMD file to it") +
                          Format("[D] Disassemble this CMD file.");
            }

            return PadScreen(Encoding.ASCII.GetBytes(
                Header($"{ProductInfo.ProductName} CMD File Manager") +
                fileInfo +
                Separator() +
                warning +
                options
                ));
        }
        private bool Import()
        {
            string path = Settings.LastCmdFile;

            if (String.IsNullOrWhiteSpace(path))
                path = Path.Combine(Storage.AppDataPath, @"CMD Files\");

            path = Dialogs.GetCommandFilePath(path);

            if (path.Length > 0)
            {
                CmdFile = new CmdFile(path);

                if (CmdFile.Valid)
                {
                    Settings.LastCmdFile = path;
                    return true;
                }
                else
                {
                    Dialogs.AlertUser("CMD File import failed: file not valid");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        private bool Load()
        {
            if (!(CmdFile is null) || Import())
            {
                if (CmdFile?.Valid ?? false)
                    return Computer.LoadCMDFile(CmdFile);
                else
                    return false;
            }
            else
            {
                return false;
            }
        }
        private void Run()
        {
            if (!(CmdFile is null) || Import())
            {
                if (Load())
                {
                    Computer.Start();
                    CurrentMode = ViewMode.Normal;
                }
                else
                {
                    MessageCallback("Failed to run CMD file");
                }
            }
        }
        private void Clear()
        {
            CmdFile = null;
        }
        private void Disassemble()
        {
            if (CmdFile is null)
            {
                MessageCallback("Can't disassemble: No CMD File loaded");
            }
            else
            {
                Load();
                var txt = String.Join(Environment.NewLine + Environment.NewLine,
                                      CmdFile.Segments.Select(s => Computer.Disassemble(s.Address, (ushort)(s.Address + s.Bytes.Count), Z80.DisassemblyMode.Normal)));
                var path = Path.Combine(Storage.AppDataPath,
                                        Path.GetFileNameWithoutExtension(CmdFile.FilePath) + ".txt")
                                            .MakeUniquePath();
                File.WriteAllText(path, txt);
                InvokeUserCommand(UserCommand.Window);
                Dialogs.ShowTextFile(path);
            }
        }
    }
}
