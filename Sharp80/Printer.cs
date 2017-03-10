/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80
{
    internal class Printer : IDisposable
    {
        private StringBuilder printBuffer = new StringBuilder();
        private bool isDisposed = false;
        private bool hasUnsavedContent = false;

        public string FilePath { get; private set; } = null;

        public bool HasContent
        {
            get { return printBuffer.Length > 0; }
        }
        public bool HasUnsavedContent
        {
            get { return hasUnsavedContent; }
        }
        public byte PrinterStatus
        {
            get
            {
                // not busy, not out of paper, selected, no fault
                return 0x30;
            }
        }
        public void Print(byte b)
        {
            switch (b)
            {
                case 0x0D:
                    printBuffer.AppendLine();
                    break;
                default:
                    printBuffer.Append((char)b);
                    hasUnsavedContent = true;
                    break;
            }
        }
        public string PrintBuffer
        {
            get { return printBuffer.ToString(); }
        }
        public void Reset()
        {
            FilePath = null;
            printBuffer = new StringBuilder();
        }
        public bool Save()
        {
            if (hasUnsavedContent)
            {
                FilePath = FilePath ?? Storage.GetUniquePath(Storage.AppDataPath, "Printer", "txt");
                Storage.SaveTextFile(FilePath,
                                     printBuffer.ToString());
                hasUnsavedContent = false;
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Dispose()
        {
            if (!isDisposed)
            {
                if (hasUnsavedContent)
                    Save();
                isDisposed = true;
            }
        }
    }
}
