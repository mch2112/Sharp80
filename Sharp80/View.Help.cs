// Sharp 80 (c) Matthew Hamilton
// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80
{
    internal class ViewHelp : View
    {
        private int ScreenNum { get; set; }
        private const int NUM_SCREENS = 5;
        private string helpHeaderText = "Sharp 80 Help";
        private string footerText = "Left/Right Arrow: Show More Commands";

        protected override ViewMode Mode => ViewMode.Help;
        protected override bool CanSendKeysToEmulation => false;
        protected override bool ForceRedraw => false;

        protected override byte[] GetViewBytes()
        {
            switch (ScreenNum)
            {
                case 0:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "BASIC COMMANDS (1/5)") +
                                        Indent("[F8]        Run / Pause") +
                                        Format() +
                                        Indent("[F1]        Show This Help") +
                                        Indent("[F2]        Show Options") +
                                        Indent("[F3]        Floppy Disk Manager") +
                                        Indent("[F4]        Tape Manager") +
                                        Format() +
                                        Indent("[Alt]+[S]                 Toggle Sound") +
                                        Indent("[Ctrl]+[+] / [Ctrl]+[-]   Zoom In / Out") +
                                        Format() +
                                        Footer(footerText)));
                case 1:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "MORE BASIC COMMANDS (2/5)") +
                                        Indent("[F5]                  Show / Hide CPU Internal Info") +
                                        Indent("[F9]                  Single Step (when paused)") +
                                        Format() +
                                        Indent("[Alt]+[P]             Save and Show Printer Output") +
                                        Indent("[Alt]+[Shift]+[P]     Save and Reset Printer") +
                                        Format() +
                                        Indent("[Alt]+[End]           Reset Button") +
                                        Indent("[Alt]+[Shift]+[End]   Hard Reset (Power Cycle)") +
                                        Indent("[Alt]+[Shift]+[X]     Exit") +
                                        Format() +
                                        Footer(footerText)));
                case 2:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "TRS-80 KEYBOARD HELP (3/5)") +
                                        Format(new string[] { "Keyboard Key", "Virtual TRS-80 Key" }, true) +
                                        Format(new string[] { "-------------------", "------------------" }, true) +
                                        Format(new string[] { "[Esc]", "[Break]           " }, true) +
                                        Format(new string[] { "[Home] or [`]", "[Clear]           " }, true) +
                                        Format() +
                                        Format(new string[] { "[\\] or [Shift]+[2]", "[@]               " }, true) +
                                        Format(new string[] { "[Caps Lock]", "[Shift]+[0]       " }, true) +
                                        Format() +
                                        Format(new string[] { "[Alt]+[End]", "Reset Button      " }, true) +
                                        Format() +
                                        Footer(footerText)));
                case 3:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "DISK COMMANDS (4/5)") +
                                        Indent("[F3]               Floppy Disk Manager") +
                                        Format() +
                                        Indent("[Alt]+[B]          Create Blank Formatted Floppy") +
                                        Indent("[Alt]+[U]          Create Unformatted Floppy") +
                                        Format() +
                                        Indent("[Alt]+[Shift]+[N]  Save Snapshot File") +
                                        Indent("[Alt]+[N]          Load Snapshot File") +
                                        Format() +
                                        Indent("[Alt]+[C]          Load CMD file") +
                                        Indent("[Alt]+[Q]          Create Floppy from File") +
                                        Footer(footerText)));
                case 4:
                    return PadScreen(Encoding.ASCII.GetBytes(
                                        Header(helpHeaderText, "ADVANCED COMMANDS (5/5)") +
                                        Format(new string[] { "[F9] Single Step", "[F10] Step Over", "[F11] Step Out" }, true) +
                                        Format(new string[] { "[F7] Set/Clear Breakpoint", "[Alt]+[V] Trace On / Off" }, true) +
                                        Format() +
                                        Indent("[F6] Jump to Address") +
                                        Format() +
                                        Indent("[Alt]+[Y] Invoke Z80 Assembler") +
                                        Indent("[Alt]+[E] Dump Memory Disassembly to File") +
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
                case KeyCode.F9:
                    // Note: doesn't consume key event
                    if (!Computer.IsRunning)
                        CurrentMode = ViewMode.Normal;
                    return base.processKey(Key);
                default:
                    return base.processKey(Key);
            }
            Invalidate();
            return true;
        }
    }
}
