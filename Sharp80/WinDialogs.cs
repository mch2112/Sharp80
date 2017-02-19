using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sharp80
{
    internal static class Dialogs
    {
        public static IWin32Window Parent { get; set; }
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
    }
}
