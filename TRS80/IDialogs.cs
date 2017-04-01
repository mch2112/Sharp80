/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    public delegate void DialogDelegate();

    public interface IDialogs
    {
        string ClipboardText { get; set; }

        void AlertUser(string Alert, string Caption = "Sharp 80");
        bool AskYesNo(string Question, string Caption = "Sharp 80");
        bool? AskYesNoCancel(string Question, string Caption = "Sharp 80");
        void ExceptionAlert(Exception Ex, string Message = "", string Caption = "Sharp 80");
        void InformUser(string Information, string Caption = "Sharp 80");
        void ShowTextFile(string Path);

        string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog);
        string GetAssemblyFile(string DefaultPath, bool Save);
        string GetCommandFilePath(string DefaultPath);
        string GetFilePath(string DefaultPath);
        string GetSnapshotFile(string DefaultPath, bool Save);
        string GetTapeFilePath(string DefaultPath, bool Save);
    }
}