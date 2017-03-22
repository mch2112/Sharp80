using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
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

        private string runWarning = Format("WARNING: The computer hasn't started yet.") +
                                    Format("Some CMD files could fail. Hit [F8] to start.") +
                                    Separator();

        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed && Key.IsUnmodified)
            {
                Invalidate();
                switch (Key.Key)
                {
                    case KeyCode.C:
                        Clear();
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
                           Indent("No CMD File Imported.") +
                           Format();

                options = Format("[R] Run CMD file") +
                          Format() +
                          Format("[L] Load CMD file without running");

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
                                                CmdFile.NumBlocks,
                                                CmdFile.NumBlocks == 1 ? String.Empty : "s",
                                                CmdFile.LowAddress.ToHexString(),
                                                CmdFile.HighAddress.ToHexString())) +
                           Format(CmdFile.ExecAddress.HasValue ? ("Execution Address: " + CmdFile.ExecAddress.Value.ToHexString()) : "NO EXECUTION ADDRESS!");

                if (!Computer.HasRunYet)
                {
                    warning = runWarning;
                }

               options = Format("[R] Run CMD file") +
                         Format() +
                         Format("[L] Reload CMD file") +
                         Format() +
                         Format("[C] Clear this CMD file") +
                         Format() +
                         Format("[F] Create a floppy and write this CMD file to it");
            }

            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 CMD File Manager") + 
                fileInfo +
                Separator() +
                warning +
                options
                ));
        }
        private bool Import()
        {
            string Path = Dialogs.GetCommandFilePath(Settings.LastCmdFile);

            if (Path.Length > 0)
            {
                CmdFile = new CmdFile(Path);

                if (CmdFile.Valid)
                {
                    Settings.LastCmdFile = Path;
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
    }
}
