/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

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
            switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
            {
                case DialogResult.Yes:
                    return true;
                default:
                    return false;
            }
        }
        public static bool? AskYesNoCancel(string Question, string Caption = "Sharp 80")
        {
            switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
                default:
                    return null;
            }
        }
        public static void InformUser(string Information, string Caption = "Sharp 80")
        {
            MessageBox.Show(Parent, Information, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        public static void AlertUser(string Alert, string Caption = "Sharp 80")
        {
            MessageBox.Show(Parent, Alert, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        public static string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog)
        {
            string dir = DefaultPath.Length > 0 ? Path.GetDirectoryName(DefaultPath) :
                                                  Path.GetDirectoryName(Application.ExecutablePath);

            FileDialog dialog;

            if (Save)
            {
                var saveDialog = new SaveFileDialog();

                dialog = saveDialog;
                saveDialog.OverwritePrompt = true;
            }
            else
            {
                var openDialog = new OpenFileDialog();
                dialog = openDialog;

                openDialog.Multiselect = false;
                openDialog.CheckPathExists = true;
            }
            dialog.Title = Title;
            dialog.FileName = (SelectFileInDialog && DefaultPath.Length > 0) ? System.IO.Path.GetFileName(DefaultPath) : string.Empty;
            dialog.InitialDirectory = dir;
            dialog.ValidateNames = true;
            dialog.Filter = Filter;
            dialog.AddExtension = true;
            dialog.DefaultExt = DefaultExtension;

            DialogResult dr = dialog.ShowDialog(Parent);

            string path = dialog.FileName;

            if (dr == DialogResult.OK && path.Length > 0)
                if (Save || File.Exists(path))
                    return path;

            return string.Empty;
        }
    }
}
