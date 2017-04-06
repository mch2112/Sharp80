/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Windows.Forms;

using Sharp80.TRS80;

namespace Sharp80
{
    public class WinDialogs : IDialogs
    {
        /// <summary>
        /// Guarantee that AfterShowDialog will always be invoked
        /// after BeforeShowDialog. Nesting is possible.
        /// </summary>

        private IWin32Window Parent { get; set; }
        private Views.IProductInfo ProductInfo { get; set; }
        private event Action BeforeShowDialog;
        private event Action AfterShowDialog;

        public WinDialogs(IWin32Window Parent, Views.IProductInfo ProductInfo, Action BeforeShowDialog, Action AfterShowDialog)
        {
            this.Parent = Parent;
            this.ProductInfo = ProductInfo;
            this.BeforeShowDialog += BeforeShowDialog;
            this.AfterShowDialog += AfterShowDialog;
        }

        // MESSAGE BOXES

        public void ExceptionAlert(Exception Ex, string Message = "", string Caption = null)
        {
            if (MainForm.IsUiThread)
            {
                Caption = Caption ?? ProductInfo.ProductName;
                try
                {
                    BeforeShowDialog?.Invoke();
                    switch (MessageBox.Show(Parent,
                                            "An exception has occurred. Copy details to clipboard?",
                                            Caption,
                                            MessageBoxButtons.YesNo,
                                            MessageBoxIcon.Question,
                                            MessageBoxDefaultButton.Button1))
                    {
                        case DialogResult.Yes:
                            ClipboardText = Ex.ToReport();
                            break;
                        default:
                            break;
                    }
                }
                finally
                {
                    AfterShowDialog?.Invoke();
                }
            }
            else
            {
                MainForm.Instance.Invoke(new Action(() => { ExceptionAlert(Ex, Message, Caption); }));
            }
        }
        public bool AskYesNo(string Question, string Caption = null)
        {
            if (MainForm.IsUiThread)
            {
                Caption = Caption ?? ProductInfo.ProductName;

                bool res;
                try
                {
                    BeforeShowDialog?.Invoke();
                    switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
                    {
                        case DialogResult.Yes:
                            res = true;
                            break;
                        default:
                            res = false;
                            break;
                    }
                }
                finally
                {
                    AfterShowDialog?.Invoke();
                }
                return res;
            }
            else
            {
                return (bool)MainForm.Instance.Invoke(new Func<bool>(
                            () => { return AskYesNo(Question, Caption); }));
            }
        }
        public bool? AskYesNoCancel(string Question, string Caption = null)
        {
            if (MainForm.IsUiThread)
            {
                Caption = Caption ?? ProductInfo.ProductName;

                bool? res;
                BeforeShowDialog?.Invoke();
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
                AfterShowDialog?.Invoke();
                return res;
            }
            else
            {
                return (bool?)MainForm.Instance.Invoke(new Func<bool?>(
                            () => { return AskYesNoCancel(Question, Caption); }));

            }
        }
        public void InformUser(string Information, string Caption = null)
        {
            if (MainForm.IsUiThread)
            {
                Caption = Caption ?? ProductInfo.ProductName;
                try
                {
                    BeforeShowDialog?.Invoke();
                    MessageBox.Show(Parent, Information, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    AfterShowDialog?.Invoke();
                }
            }
            else
            {
                MainForm.Instance.Invoke(new Action(() => { InformUser(Information, Caption); }));
            }
        }
        public void AlertUser(string Alert, string Caption = null)
        {
            if (MainForm.IsUiThread)
            {
                Caption = Caption ?? ProductInfo.ProductName;

                try
                {
                    BeforeShowDialog?.Invoke();
                    MessageBox.Show(Parent, Alert, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    AfterShowDialog?.Invoke();
                }
            }
            else
            {
                MainForm.Instance.Invoke(new Action(() => { AlertUser(Alert, Caption); }));
            }
        }

        // PATHS AND FILE DIALOGS

        public string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            string dir = DefaultPath.Length > 0 ? Path.GetDirectoryName(DefaultPath) :
                                                  Storage.DocsPath;

            if (!Directory.Exists(dir))
                dir = Storage.DocsPath;

            if (!Save && !File.Exists(DefaultPath))
                DefaultPath = String.Empty;

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
                };
            }
            dialog.Title = Title;
            dialog.FileName = (SelectFileInDialog && DefaultPath.Length > 0) ? Path.GetFileName(DefaultPath) : null;
            dialog.InitialDirectory = dir;
            dialog.ValidateNames = true;
            dialog.Filter = Filter;
            dialog.AddExtension = true;
            dialog.DefaultExt = DefaultExtension;
            dialog.CheckFileExists = !Save;
            dialog.CheckPathExists = true;

            DialogResult dr;
            try
            {
                BeforeShowDialog?.Invoke();
                dr = dialog.ShowDialog(Parent);
            }
            finally
            {
                AfterShowDialog?.Invoke();
            }

            string path = dialog.FileName;

            if (dr == DialogResult.OK && path.Length > 0)
                if (Save || File.Exists(path))
                    return path;

            return string.Empty;
        }
        public string GetFilePath(string DefaultPath)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select File",
                                  Filter: "TRS-80 Files (*.cmd; *.bas; *.txt)|*.cmd;*.bas;*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        public string GetCommandFilePath(string DefaultPath)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select CMD File",
                                  Filter: "TRS-80 CMD Files (*.cmd)|*.cmd|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        public string GetSnapshotFile(string DefaultPath, bool Save)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            if (string.IsNullOrWhiteSpace(DefaultPath))
                DefaultPath = Path.Combine(Storage.AppDataPath, @"Snapshots/");

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Snapshot File" : "Load Snapshot File",
                                  Filter: "TRS-80 Snapshot Files (*.snp)|*.snp|All Files (*.*)|*.*",
                                  DefaultExtension: "snp",
                                  SelectFileInDialog: !Save);
        }
        public string GetAssemblyFile(string DefaultPath, bool Save)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Assembly File" : "Load Assembly File",
                                  Filter: "Z80 Assembly Files (*.asm)|*.asm|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "asm",
                                  SelectFileInDialog: true);
        }
        public string GetTapeFilePath(string DefaultPath, bool Save)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Tape File" : "Load Tape File",
                                  Filter: "Cassette Files (*.cas)|*.cas|All Files (*.*)|*.*",
                                  DefaultExtension: "cas",
                                  SelectFileInDialog: true);
        }

        // TEXT FILE LAUNCHING

        public void ShowTextFile(string Path)
        {
            if (Path.ToUpper().EndsWith(".TXT"))
                System.Diagnostics.Process.Start(Path);
        }

        // CLIPBOARD

        public string ClipboardText
        {
            get => Clipboard.GetText(TextDataFormat.Text);
            set => Clipboard.SetText(value, TextDataFormat.Text);
        }
    }
}
