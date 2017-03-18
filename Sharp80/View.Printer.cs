using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class ViewPrinter : View
    {
        private const int NUM_DISPLAY_LINES = 6;

        protected override ViewMode Mode => ViewMode.Printer;
        protected override bool CanSendKeysToEmulation => false;
        protected override bool ForceRedraw => FrameReqNum % 30 == 0;

        private string[] Lines = new string[0];
        private int curLine = 0;

        protected override void Activate()
        {
            RefreshPrinterContent();
            base.Activate();
        }
        protected override bool processKey(KeyState Key)
        {
            Invalidate();
			if (Key.Pressed && Key.IsUnmodified)
            {
                if (Computer.PrinterHasContent)
                {
                    switch (Key.Key)
                    {
                        case KeyCode.S:
                            ShowPrinterOutput();
                            break;
                        case KeyCode.D:
                            Computer.PrinterReset();
                            break;
                    }
                }
            }
            return base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            string printContent;
            string options;
            if (Computer.PrinterHasContent)
            {
                printContent = ViewPrinterOutput();
                options = Format("[S] Save to Text File") +
                          Format("[D] Delete Print Job");

                if (Lines.Count() > NUM_DISPLAY_LINES)
                    options += Format("[Up Arrow] / [Down Arrow] Scroll Output");

            }
			else
            {
                printContent = Format() +
							   Format() +
                               Indent("No printer output.") +
                               Format().Repeat(NUM_DISPLAY_LINES - 3);
                options = String.Empty;
            }
            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 Z80 Pritner Status") +
                printContent +
				Separator() +
				options));

        }
		private string[] GetPrinterOutput()
        {
            return Computer.PrinterContent.Split(new string[] { "\r\n" }, StringSplitOptions.None).Select(l => Format(l).Truncate(ScreenMetrics.NUM_SCREEN_CHARS_X)).ToArray();
        }
		private string ViewPrinterOutput()
        {
            return String.Join(String.Empty,
							   Lines.Skip(curLine).Take(NUM_DISPLAY_LINES).Select(l => Format(l))) +
							   Format().Repeat(NUM_DISPLAY_LINES - Lines.Count());
        }
		private void RefreshPrinterContent()
        {
            Lines = GetPrinterOutput();
        }
        private bool ShowPrinterOutput()
        {
            if (Computer.PrinterHasContent)
            {
                Computer.PrinterSave();
                Dialogs.ShowTextFile(Computer.PrinterFilePath);
                return true;
            }
            else
            {
                Dialogs.AlertUser("Nothing printed yet.");
                return false;
            }
        }
    }
}
