// Sharp 80 (c) Matthew Hamilton
// Licensed Under GPL v3

using System;
using System.Text;

namespace Sharp80
{
    internal class ViewHelp : View
    {
        private int ScreenNum { get; set; }
        private const int NUM_SCREENS = 4;
        private string helpHeaderText = "Sharp 80 Help";
        private string footerText = "[Left/Right Arrow] Show More Commands";

        protected override ViewMode Mode => ViewMode.HelpView;

        protected override bool ForceRedraw => false;
        protected sealed override byte[] GetViewBytes()
        {
            switch (ScreenNum)
            {
                case 0:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "BASIC COMMANDS (1/4)") +
                                        Indent("[F1] Show This Help") +
                                        Indent("[F2] Show Options") +
                                        Indent("[F3] Floppy Disk Manager") +
                                        Indent("[F4] Show / Hide Z80 CPU Internal Info") +
                                        Format() +
                                        Format(new string[] { "[F8] Run / Pause", "[F9] Single Step" }, true) +
                                        Format() + 
                                        Indent("[Control]+[+] / [Control]+[-]   Zoom In / Out") +
                                        Indent("[Shift]+[Alt]+[End]             Hard Reset (Power Cycle)") +
                                        Indent("[Shift]+[Alt]+[X]               Exit") +
                                        Footer(footerText)));
                case 1:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "TRS-80 KEYBOARD HELP (2/4)") +
                                        Format(new string[] { "Keyboard Key", "Virtual TRS-80 Key" }, true) +
                                        Format(new string[] { "-------------------", "------------------" }, true) +
                                        Format(new string[] { "[Esc]", "[Break]           " }, true) +
                                        Format(new string[] { "[Home]", "[Clear]           " }, true) +
                                        Format() +
                                        Format(new string[] { "[\\] or [Shift]+[2]", "[@]               " }, true) +
                                        Format(new string[] { "[Caps Lock]", "[Shift] + [0]     " }, true) +
                                        Format() +
                                        Format(new string[] { "[Alt]+[End]", "Reset Button      " }, true) +
                                        Format() +
                                        Footer(footerText)));
                case 2:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "DISK COMMANDS (3/4)") +
                                        Indent("[F3] Floppy Disk Manager") +
                                        Format() +
                                        Indent("[Alt]+[F] Create Formatted Floppy") +
                                        Indent("[Alt]+[U] Create Unformatted Floppy") +
                                        Format() +
                                        Indent("[Shift]+[Alt]+[N] Save Snapshot File") +
                                        Indent("[Alt]+[N]         Load Snapshot File") +
                                        Format() +
                                        Indent("[Alt]+[C] Load CMD file") +
                                        Indent("[Alt]+[Z] Create Floppy from File") +
                                        Footer(footerText)));
                case 3:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "ADVANCED COMMANDS (4/4)") +
                                        Format(new string[] { "[F9] Single Step", "[F10] Step Over", "[F11] Step Out" }, true) +
                                        Format(new string[] { "[F7] Set/Clear Breakpoint", "[Alt]+[E] Trace On / Off" }, true) +
                                        Format() +
                                        Indent("[F6] Jump to Address") +
                                        Format() +
                                        Indent("[Alt]+[Y] Invoke Z80 Assembler") +
                                        Indent("[Alt]+[P] Dump Memory Disassembly to File") +
                                        Format() +
                                        Format(new string[] { "[Alt]+[M] Memory Viewer" }, true) +
                                        Format(new string[] { "[Alt]+[I] Instruction Set Report" }, true) +
                                        Footer(footerText)));
                default:
                    throw new Exception("Invalid Help View");
            }
        }
        protected override bool processKey(KeyState Key)
        {
            if (Key.Released)
                return base.processKey(Key);

            switch (Key.Key)
            {
                case KeyCode.Space:
                case KeyCode.Right:
                    ++ScreenNum;
                    ScreenNum %= NUM_SCREENS;
                    break;
                case KeyCode.Left:
                    ScreenNum += NUM_SCREENS - 1;
                    ScreenNum %= NUM_SCREENS;
                    break;
                case KeyCode.F8:
                    // Note: doesn't consume key event
                    if (!Computer.IsRunning)
                        CurrentMode = ViewMode.NormalView;
                    return base.processKey(Key);
                default:
                    return base.processKey(Key);
            }
            Invalidate();
            return true;
        }
    }
}
