using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp80
{
    public static class UI
    {
        public const int NUM_HELP_SCREENS = 4;

        private static byte[][] helpText = new byte[NUM_HELP_SCREENS][];

        private static int currentScreenNum = 0;
        private const int STANDARD_INDENT = 3;

        static UI()
        {
            SetupHelpText();
        }

        public static byte[] GetHelpText()
        {
            return UI.helpText[UI.currentScreenNum];
        }

        public static byte[] GetOptionsText(bool SoundOn, bool UseDriveNoise, bool GreenScreen, bool AutoStartOnReset, bool Throttle, bool Z80Display, bool HistoricDisassembly, bool FullScreen)
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                UI.Header("Sharp 80 Options - [F2] to show/hide") +
                UI.Indent(string.Format("[Alt]+[S] Sound                    {0}",
                    SoundOn ? "[ON] /  OFF" : " ON  / [OFF]")) +
                (SoundOn
                    ? UI.Indent(string.Format("[Alt]+[T] Drive Noise              {0}",
                        UseDriveNoise ? "[ON] /  OFF" : " ON  / [OFF]"))
                    : UI.Format()) +
                UI.Format() +
                UI.Indent(string.Format("[Alt]+[G] Screen Color             {0}",
                    GreenScreen ? " WHITE  / [GREEN]" : "[WHITE]  /  GREEN")) +
                UI.Format() +
                UI.Indent(string.Format("[Alt]+[A] Auto Start on Reset      {0}",
                    AutoStartOnReset ? "[ON] /  OFF" : " ON  / [OFF]")) +
                UI.Format() +
                UI.Indent(string.Format("[F12] Throttle                     {0}",
                    Throttle ? "[NORMAL] / FAST" : " NORMAL / [FAST]")) +
                UI.Format() +
                UI.Indent(string.Format("[F4] Z80 Internals Display         {0}",
                    Z80Display ? "[ON] /  OFF" : " ON  / [OFF]")) +
                (Z80Display
                    ? UI.Indent(string.Format("[Alt]+[H] Disassembly Mode         {0}",
                        !HistoricDisassembly ? "[NORMAL] /  HISTORIC" : " NORMAL  / [HISTORIC]"))
                    : UI.Format()) +
                UI.Format() +
                UI.Indent(string.Format("[Alt]+[Enter] Full-Screen View     {0}",
                    FullScreen ? " ON  / [OFF]" : "[ON] /  OFF ")) +
                UI.Format()));
        }

        internal static byte[] GetDiskView(FloppyController FC, byte? FloppyNum)
        {
            string s = UI.Header("Sharp 80 Floppy Disk Status - [F3] to show or hide");
            if (FloppyNum.HasValue)
            {
                bool diskLoaded = !FC.DriveIsUnloaded(FloppyNum.Value);
                s +=    DrawDisk(FC, FloppyNum.Value) +
                        UI.Format() +
                        UI.Separator('-') +
                        UI.Format() +
                        UI.Format(string.Format("Drive {0} Commands", FloppyNum.Value)) +
                        UI.Format() +
                        UI.Format("[L] Load Floppy from file");

                if (diskLoaded)
                {
                    s += UI.Format("[E] Eject floppy") +
                         UI.Format(string.Format("[W] Toggle write protection {0}", FC.IsWriteProtected(FloppyNum.Value).Value ? "[ON] /  OFF " : " ON  / [OFF]")) +
                         UI.Format("[Z] Disk Zap View");
                }
                else
                {
                    s += UI.Format("[B] Insert blank formatted floppy") +
                         UI.Format("[U] Insert unformatted floppy");
                }
                s += UI.Format() +
                     UI.Format("[Escape] Back to all drives");
            }
            else
            {
                for (byte i = 0; i < 4; i++)
                    s += DrawDisk(FC, i) + Format();

                s += Format("Choose a floppy drive [0] to [3].") +
                     Format("[Escape] to cancel.");
            }

            return PadScreen(Encoding.ASCII.GetBytes(s));
        }

        private static string DrawDisk(FloppyController fc, byte DiskNum)
        {
            var d = fc.GetFloppy(DiskNum);

            string line1;
            if (d.IsEmpty)
                line1 = string.Format("Drive #{0}: Unloaded", DiskNum);
            else
                line1 = string.Format("Drive #{0}: {1} | {2} | {3} Tks | {4} Sec{5}{6}",
                                      DiskNum,
                                      ((d.NumSides > 1) ? "Dbl Side" : "Sgl Side"),
                                      (d.DoubleDensity.HasValue ? (d.DoubleDensity.Value ? "Dbl Den" : "Sgl Den") : "Mix Den"),
                                      d.NumTracks,
                                      d.SectorsPerTrack,
                                      d.WriteProtected ? " | WP" : string.Empty,
                                      d.Formatted ? string.Empty : " | UNFORMATTED");

            string line2;
            if (d.IsEmpty)
                line2 = String.Empty;
            else
                line2 = FitFilePath(d.FilePath);

            return UI.Format(line1) + UI.Format(line2);
        }

        public static byte[] GetBreakpointText(ushort Breakpoint, bool On)
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                UI.Header("Set breakpoint location") +
                UI.Format() +
                UI.Indent("Breakpoint is currently " + (On ? "ON" : "OFF")) +
                UI.Indent("Breakpoint Value (Hexadecimal): " + Lib.ToHexString(Breakpoint)) +
                UI.Format() + 
                UI.Separator() +
                UI.Indent("Type [0]-[9] or [A]-[F] to enter a hexadecimal") +
                UI.Indent("breakpoint location.") +
                UI.Format() +
                UI.Indent("[Space Bar] to toggle breakpoint on and off.") +
                UI.Format() +
                UI.Indent("[Enter] when done.")));
        }

        public static byte[] GetJumpToText(ushort JumpVector)
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                UI.Header("Jump to Z80 memory location") +
                UI.Format() +
                UI.Indent("Jump to memory location (Hexadecimal): " + Lib.ToHexString(JumpVector)) +
                UI.Format() +
                UI.Separator() +
                UI.Indent("Type [0]-[9] or [A]-[F] to enter a hexadecimal") +
                UI.Indent("jump location.") +
                UI.Format() +
                UI.Indent("[Enter] when done.")));
        }

        public static void AdvanceHelp()
        {
            UI.currentScreenNum = (UI.currentScreenNum + 1) % UI.NUM_HELP_SCREENS;
        }

        private static void SetupHelpText()
        {
            string helpHeaderText = "Sharp 80 Help - [F1] to show/hide";
            string footerText = "[Space] Show More Commands";
            UI.helpText[0] = PadScreen(Encoding.ASCII.GetBytes(
                UI.Header(helpHeaderText, "BASIC COMMANDS (1/4)") +
                UI.Indent("[F8] Run / Pause") +
                UI.Indent("[F9] Single Step") +
                UI.Format() +
                UI.Indent("[F2] Show Options") +
                UI.Indent("[F3] Floppy Disk Manager") +
                UI.Indent("[F4] Show / Hide Z80 Internals Info") +
                UI.Format() +
                UI.Indent("[Shift]+[Alt]+[End] Hard Reset (Power Cycle)") +
                UI.Format() +
                UI.Indent("[Shift]+[Alt]+[X] Exit") +
                UI.Footer(footerText)));

            UI.helpText[1] = PadScreen(Encoding.ASCII.GetBytes(
                UI.Header(helpHeaderText, "KEYBOARD HELP (2/4)") +
                UI.Format(new string[] { "Keyboard Key",        "Virtual TRS-80 Key" }, true) +
                UI.Format(new string[] { "-------------------", "------------------" }, true) +
                UI.Format(new string[] { "[Esc]",               "[Break]           " }, true) +
                UI.Format(new string[] { "[Home]",              "[Clear]           " }, true) +
                UI.Format() +
                UI.Format(new string[] { "[\\] or [Shift]+[2]", "[@]               " }, true) +
                UI.Format(new string[] { "[Caps Lock]",         "[Shift] + [0]     " }, true) +
                UI.Format() +
                UI.Format(new string[] { "[Shift]+[End]",       "Reset Button      " }, true) +
                UI.Format() +
                UI.Footer(footerText)));

            UI.helpText[2] = PadScreen(Encoding.ASCII.GetBytes(
                UI.Header(helpHeaderText, "DISK COMMANDS (3/4)") +
                UI.Indent("[F3] Floppy Disk Manager") +
                UI.Format() +
                UI.Indent("[Alt]+[F] Create Formatted Floppy") +
                UI.Indent("[Alt]+[U] Create Unformatted Floppy") +
                UI.Format() +
                UI.Indent("[Shift]+[Alt]+[N] Save Snapshot File") +
                UI.Indent("[Alt]+[N]         Load Snapshot File") +
                UI.Format() +
                UI.Indent("[Alt]+[C] Load CMD file") +
                UI.Indent("[Alt]+[Z] Create Floppy from File") +
                UI.Footer(footerText)));

            UI.helpText[3] = PadScreen(Encoding.ASCII.GetBytes(
                UI.Header(helpHeaderText, "ADVANCED COMMANDS (4/4)") +
                UI.Format(new string[] {"[F9] Single Step", "[F10] Step Over", "[F11] Step Out"}, true) +
                UI.Format(new string[] {"[F7] Set/Clear Breakpoint", "[Alt]+[E] Trace On / Off"}, true) +
                UI.Format() +
                UI.Indent("[F6] Jump to Address") +
                UI.Format() +
                UI.Indent("[Alt]+[Y] Invoke Z80 Assembler") +
                UI.Indent("[Alt]+[P] Dump Memory Disassembly to File") +
                UI.Format() +
                UI.Format(new string[] {"[Alt]+[M] Memory Viewer"}, true) +
                UI.Format(new string[] {"[Alt]+[I] Instruction Set Report"}, true) +
                UI.Footer(footerText)));

            for (int i = 0; i < UI.NUM_HELP_SCREENS; i++)
                Debug.Assert(UI.helpText[i].Length == ScreenDX.NUM_SCREEN_CHARS_X * ScreenDX.NUM_SCREEN_CHARS_Y);
        }

        private static string Center(string Input)
        {
            Debug.Assert(Input.Length <= ScreenDX.NUM_SCREEN_CHARS_X);

            return Input.PadLeft((ScreenDX.NUM_SCREEN_CHARS_X + Input.Length) / 2).PadRight(ScreenDX.NUM_SCREEN_CHARS_X);
        }

        private static string Format(string Input)
        {
            return Format(Input, 0);
        }

        private static string Format(string Input, int Indent)
        {
            Debug.Assert((Input.Length + Indent) <= ScreenDX.NUM_SCREEN_CHARS_X);

            return (new String(' ', Indent) + Input).PadRight(ScreenDX.NUM_SCREEN_CHARS_X);
        }

        private static string Format(string[] Input, bool Indent)
        {
            switch (Input.Length)
            {
                case 0:
                    return Format();
                case 1:
                    if (Indent)
                        return UI.Indent(Input[0]);
                    else
                        return Format(Input[0]);
                default:

                    int inputLength = Input.Sum(s => s.Length);

                    int extraSpace = ScreenDX.NUM_SCREEN_CHARS_X - inputLength - (Indent ? 2 * STANDARD_INDENT : 0);

                    Debug.Assert(extraSpace >= 0);

                    int numGaps = Input.Length - 1;

                    int minGapLength = (int) Math.Floor((decimal) extraSpace / (decimal) numGaps);
                    int maxGapLength = (int) Math.Ceiling((decimal) extraSpace / (decimal) numGaps);
                    string minGap = new String(' ', minGapLength);
                    string maxGap = new String(' ', maxGapLength);

                    int numMax = extraSpace - numGaps * minGapLength;

                    var sb = new StringBuilder();

                    if (Indent)
                        sb.Append(new String(' ', STANDARD_INDENT));
                    for (int i = 0; i < Input.Length; i++)
                    {
                        sb.Append(Input[i]);
                        if (i < Input.Length - 1)
                            if (numMax-- > 0)
                                sb.Append(maxGap);
                            else
                                sb.Append(minGap);
                    }
                    if (Indent)
                        sb.Append(new String(' ', STANDARD_INDENT));
                    return sb.ToString();
            }
        }

        private static string Format()
        {
            return Format("");
        }

        private static string Indent(string Input)
        {
            return Format(Input, STANDARD_INDENT);
        }

        private static string Separator(char Char = '=')
        {
            return new String(Char, ScreenDX.NUM_SCREEN_CHARS_X);
        }

        private static string Header(string HeaderText, string SubHeaderText = "")
        {
            return Center(HeaderText) +
                   Separator() +
                   (SubHeaderText.Length > 0 ? Center(SubHeaderText) + Format() : String.Empty);
        }

        private static string Footer(string Text)
        {
            return Separator() +
                   Center(Text);
        }

        private static byte[] PadScreen(byte[] Screen)
        {
            if (Screen.Length == ScreenDX.NUM_SCREEN_CHARS)
                return Screen;
            else
            {
                byte[] s = new byte[ScreenDX.NUM_SCREEN_CHARS];
                Array.Copy(Screen, s, Math.Min(ScreenDX.NUM_SCREEN_CHARS, Screen.Length));
                return s;
            }
        }
        private static string FitFilePath(string FilePath)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                return "<Untitled>";
            else if (FilePath.Length <= ScreenDX.NUM_SCREEN_CHARS_X)
                return FilePath;
            else
                return FilePath.Substring(0, 20) + "..." +
                       FilePath.Substring(FilePath.Length - ScreenDX.NUM_SCREEN_CHARS_X + 23);
        }
    }
}