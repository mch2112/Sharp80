/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.IO;
using System.Windows.Forms;

namespace Sharp80
{
    internal static class Dialogs
    {
        private static IWin32Window Parent { get; set; }

        public static void Initialize(IWin32Window Parent)
        {
            Dialogs.Parent = Parent;
        }

        public static bool AskYesNo(string Question, string Caption = "Sharp 80")
        {
            bool res;
            ForceShowCursor();

            switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
            {
                case DialogResult.Yes:
                    res = true;
                    break;
                default:
                    res = false;
                    break;
            }
            NoForceShowCursor();
            return res;
        }
        public static bool? AskYesNoCancel(string Question, string Caption = "Sharp 80")
        {
            bool? res;
            ForceShowCursor();
            switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
            {
                case DialogResult.Yes:
                    res = true;
                    break;
                case DialogResult.No:
                    res = false;
                    break;
                default:
                    res = null;
                    break;
            }
            NoForceShowCursor();
            return res;
        }
        public static void InformUser(string Information, string Caption = "Sharp 80")
        {
            ForceShowCursor();
            MessageBox.Show(Parent, Information, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            NoForceShowCursor();
        }
        public static void AlertUser(string Alert, string Caption = "Sharp 80")
        {
            ForceShowCursor();
            MessageBox.Show(Parent, Alert, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            NoForceShowCursor();
        }
        public static string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog)
        {
            string dir = DefaultPath.Length > 0 ? Path.GetDirectoryName(DefaultPath) :
                                                  Storage.DocsPath;

            FileDialog dialog;

            if (Save)
            {
                dialog = new SaveFileDialog()
                {
                    OverwritePrompt = true
                };
            }
            else
            {
                dialog = new OpenFileDialog()
                {
                    Multiselect = false,
                    CheckPathExists = true
                };
            }
            dialog.Title = Title;
            dialog.FileName = (SelectFileInDialog && DefaultPath.Length > 0) ? Path.GetFileName(DefaultPath) : string.Empty;
            dialog.InitialDirectory = dir;
            dialog.ValidateNames = true;
            dialog.Filter = Filter;
            dialog.AddExtension = true;
            dialog.DefaultExt = DefaultExtension;

            ForceShowCursor();
            var dr = dialog.ShowDialog(Parent);
            NoForceShowCursor();

            string path = dialog.FileName;

            if (dr == DialogResult.OK && path.Length > 0)
                if (Save || File.Exists(path))
                    return path;

            return string.Empty;
        }
        public static string GetFilePath(string DefaultPath)
        {
            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select File",
                                  Filter: "TRS-80 Files (*.cmd; *.bas; *.txt)|*.cmd;*.bas;*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        public static string GetCommandFile(string DefaultPath)
        {
            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select CMD File",
                                  Filter: "TRS-80 CMD Files (*.cmd)|*.cmd|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        public static string GetSnapshotFile(string DefaultPath, bool Save)
        {
            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Snapshot File" : "Load Snapshot File",
                                  Filter: "TRS-80 Snapshot Files (*.snp)|*.snp|All Files (*.*)|*.*",
                                  DefaultExtension: "snp",
                                  SelectFileInDialog: true);
        }
        public static string GetAssemblyFile(string DefaultPath, bool Save)
        {
            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Assembly File" : "Load Assembly File",
                                  Filter: "Z80 Assembly Files (*.asm)|*.asm|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "asm",
                                  SelectFileInDialog: true);
        }
        public static string GetTapeFilePath(string DefaultPath, bool Save)
        {
            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Tape File" : "Load Tape File",
                                  Filter: "Cassette Files (*.cas)|*.cas|All Files (*.*)|*.*",
                                  DefaultExtension: "cas",
                                  SelectFileInDialog: true);
        }

        // CURSOR MANAGEMENT

        private static bool suppressCursor = false;
        private static int cursorLevel = 0;
        private static bool cursorHidden = false;

        public static bool SuppressCursor
        {
            get { return suppressCursor; }
            set
            {
                suppressCursor = value;
                UpdateCursor();
            }
        }
        public static void ForceShowCursor()
        {
            cursorLevel++;
            UpdateCursor();
        }
        public static void NoForceShowCursor()
        {
            cursorLevel--;
            UpdateCursor();
        }
        private static void UpdateCursor()
        {
            if (cursorLevel < 0)
                throw new Exception();

            if (suppressCursor && cursorLevel == 0)
            {
                if (!cursorHidden)
                {
                    Cursor.Hide();
                    cursorHidden = true;
                }
            }
            else if (cursorHidden)
            {
                Cursor.Show();
                cursorHidden = false;
            }
        }
        // TEXT FILE LAUNCHING

        public static void ShowTextFile(string Path)
        {
            if (Path.ToUpper().EndsWith(".TXT"))
                System.Diagnostics.Process.Start(Path);
        }
    }
}
