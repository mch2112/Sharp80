/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80.TRS80
{
    internal class Printer
    {
        private StringBuilder printBuffer = new StringBuilder();

        private bool hasUnsavedContent = false;

        // not busy, not out of paper, selected, no fault
        public byte PrinterStatus => 0x30;
        public string PrintBuffer => printBuffer.ToString();
        public bool HasContent => printBuffer.Length > 0;
        public bool HasUnsavedContent => hasUnsavedContent;

        public void Print(byte b)
        {
            switch (b)
            {
                case 0x00: // NUL
                case 0x02: // start of heading
                case 0x08: // backspace
                case 0x0F: // shift in
                case 0x11: // device control 1
                case 0x19: // end of medium
                    break;
                case 0x1B:
                    printBuffer.Append(' ');
                    break;
                case 0x0D:
                    printBuffer.AppendLine();
                    break;
                default:
                    printBuffer.Append((char)b);
                    hasUnsavedContent = true;
                    break;
            }
        }
        public void Reset() => printBuffer = new StringBuilder();
    }
}
