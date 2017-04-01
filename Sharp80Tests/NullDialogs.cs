using System;

using Sharp80.TRS80;

namespace Sharp80Tests
{
    class NullDialogs : IDialogs
    {
        public string ClipboardText { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void ExceptionAlert(Exception Ex, string Message = "", string Caption = "Sharp 80")
        {
            throw Ex;
        }
        public void AlertUser(string Alert, string Caption = "Sharp 80") { }

        public bool AskYesNo(string Question, string Caption = "Sharp 80")
        {
            throw new NotImplementedException();
        }
        public bool? AskYesNoCancel(string Question, string Caption = "Sharp 80")
        {
            throw new NotImplementedException();
        }
        public string GetAssemblyFile(string DefaultPath, bool Save)
        {
            throw new NotImplementedException();
        }
        public string GetCommandFilePath(string DefaultPath)
        {
            throw new NotImplementedException();
        }
        public string GetFilePath(string DefaultPath)
        {
            throw new NotImplementedException();
        }
        public string GetSnapshotFile(string DefaultPath, bool Save)
        {
            throw new NotImplementedException();
        }
        public string GetTapeFilePath(string DefaultPath, bool Save)
        {
            throw new NotImplementedException();
        }
        public void InformUser(string Information, string Caption = "Sharp 80")
        {
            throw new NotImplementedException();
        }
        public void ShowTextFile(string Path)
        {
            throw new NotImplementedException();
        }
        public string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog)
        {
            throw new NotImplementedException();
        }
    }
}
