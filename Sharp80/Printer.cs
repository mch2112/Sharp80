/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Text;

namespace Sharp80
{
    internal class Printer : IDisposable
    {
        private StringBuilder print = new StringBuilder();
        private bool isDisposed = false;

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
                    print.AppendLine();
                    break;
                default:
                    print.Append((char)b);
                    break;
            }
        }
    
        public override string ToString()
        {
            return print.ToString();
        }
        public void Dispose()
        {
            if (!isDisposed)
            {
                       if (print.Length > 0)
                    Storage.SaveTextFile(System.IO.Path.Combine(Storage.AppDataPath, "printer.txt"), print.ToString());
                isDisposed = true;
            }
        }
    }
}
