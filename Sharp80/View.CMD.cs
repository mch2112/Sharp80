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

        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed)
            {
                Invalidate();
                switch (Key.Key)
                {
                    case KeyCode.I:
                        ImportCMDFile();
                        return true;
                    case KeyCode.L:
                        LoadCmdFile();
                        break;
                    case KeyCode.R:
                        RunCmdFile();
                        return true;
                    case KeyCode.F8:
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

            if (CmdFile == null)
            {
                fileInfo = Format() + Indent("No CMD File Imported.") + Format();
                options = String.Empty;
            }
            else if (!CmdFile.Valid)
            {
                fileInfo = Format("CMD File: " + FitFilePath(CmdFile.FilePath, ScreenMetrics.NUM_SCREEN_CHARS_X - "CMD File: ".Length)) +
                           Format() + 
                           Format("Invalid CMD File");
                options = String.Empty;
            }
            else
            {
                fileInfo = Format("CMD File: " + FitFilePath(CmdFile.FilePath, ScreenMetrics.NUM_SCREEN_CHARS_X - "CMD File: ".Length)) +
                           Format(string.Format("{0} bytes in {1} block{2} spanning {3}:{4}", 
                                                CmdFile.Size,
                                                CmdFile.NumBlocks,
                                                CmdFile.NumBlocks == 1 ? String.Empty : "s",
                                                CmdFile.LowAddress.ToHexString(),
                                                CmdFile.HighAddress.ToHexString())) +
                           Format(CmdFile.ExecAddress.HasValue ? ("Execution Address: " + CmdFile.ExecAddress.Value.ToHexString()) : "NO EXECUTION ADDRESS!");

                options = Format("[L] Load CMD File into memory without running") +
                          Format();

                if (CmdFile.ExecAddress.HasValue)
                    options += Format("[R] Run CMD file");
                else
                    options += Format();
            }

            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 CMD File Manager") + 
                fileInfo +
                Separator() +
                Format() +
                Format("[I] Import CMD file") +
                Format() +
                options
                ));
        }
        private void ImportCMDFile()
        {
            string Path = Dialogs.GetCommandFile(Settings.LastCmdFile);

            if (Path.Length > 0)
            {
                CmdFile = new CmdFile(Path);

                if (CmdFile.Valid)
                    Settings.LastCmdFile = Path;
                else
                    Dialogs.AlertUser("CMD File load failed.");
            }
        }
        private void LoadCmdFile()
        {
            if (CmdFile?.Valid ?? false)
                Computer.LoadCMDFile(CmdFile);
        }
        private void RunCmdFile()
        {
            if (CmdFile?.Valid ?? false)
            {
                Computer.LoadCMDFile(CmdFile);
                Computer.Start();
                CurrentMode = ViewMode.Normal;
            }
        }
    }
}
