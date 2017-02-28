/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp80
{
    public enum ViewMode { NormalView, MemoryView, DiskView, HelpView, DiskZapView, SetBreakpointView, JumpToView, OptionsView, CpuView, FloppyControllerView }
    public enum UserCommand { ToggleAdvancedView, ToggleFullScreen, GreenScreen, ShowInstructionSet, ZoomIn, ZoomOut, HardReset, Exit }

    internal abstract class View
    {
        public delegate void MessageDelegate(string Message);
        public delegate void UserCommandHandler(UserCommand Command);

        public static event UserCommandHandler OnUserCommand;

        private static bool invalid = false;
        public static void Validate() { invalid = false; }
        public static bool Invalid
        {
            get
            {
                return invalid || views[CurrentMode].ForceRedraw;
            }
            private set
            {
                invalid = value;
            }
        }
        protected static void Invalidate() { invalid = true; }

        public static ViewMode CurrentMode
        {
            get { return currentMode; }
            set
            {
                if (currentMode != value)
                {
                    currentMode = value;
                    views[value].Activate();
                    Invalidate();
                }
            }
        }
        public static View GetView(ViewMode Mode) { return views[Mode]; }

        protected static Dictionary<ViewMode, View> views = new Dictionary<ViewMode, View>();
        protected static string ExecutablePath { get; private set; }
        protected abstract ViewMode Mode { get; }
        protected abstract bool ForceRedraw { get; }
        protected static Computer Computer { get; private set; }
        protected static byte? DriveNumber { get; set; } = null;
        protected static MessageDelegate MessageCallback { get; private set; }

        private static ViewMode currentMode = ViewMode.NormalView;
        private static bool initialized = false;
        private const int STANDARD_INDENT = 3;

        public static void Initialize(Computer Computer, string ExecutablePath, MessageDelegate MessageCallback)
        {
            View.Computer = Computer;
            View.ExecutablePath = ExecutablePath;
            View.MessageCallback = MessageCallback;

            if (!initialized)
            {
                new ViewNormal();
                new ViewHelp();
                new ViewOptions();
                new ViewDisk();
                new ViewZap();
                new ViewBreakpoint();
                new ViewJump();
                new ViewMemory();
                new ViewFloppyController();
                new CPUView();
                CurrentMode = ViewMode.HelpView;
                initialized = true;
            }
        }

        public View()
        {
            views.Add(Mode, this);
        }

        protected virtual void Activate() { }

        public static bool ProcessKey(KeyState Key)
        {
            return views[CurrentMode].processKey(Key);
        }
        protected virtual bool processKey(KeyState Key)
        {
            if (Key.IsUnmodified)
            {
                if (Key.Pressed && !Key.Shift)
                {
                    switch (Key.Key)
                    {
                        case KeyCode.Escape:
                            if (CurrentMode != ViewMode.NormalView)
                            {
                                CurrentMode = ViewMode.NormalView;
                                return true;
                            }
                            break;
                        case KeyCode.F1:
                            CurrentMode = ViewMode.HelpView;
                            return true;
                        case KeyCode.F2:
                            CurrentMode = ViewMode.OptionsView;
                            return true;
                        case KeyCode.F3:
                            CurrentMode = ViewMode.DiskView;
                            return true;
                        case KeyCode.F4:
                            OnUserCommand?.Invoke(UserCommand.ToggleAdvancedView);
                            MessageCallback(Settings.AdvancedView ? "Advanced View" : "Normal View");
                            return true;
                        case KeyCode.F6:
                            CurrentMode = ViewMode.JumpToView;
                            return true;
                        case KeyCode.F7:
                            CurrentMode = ViewMode.SetBreakpointView;
                            return true;
                        case KeyCode.F8:
                            if (Computer.IsRunning)
                                Computer.Stop(true);
                            else
                                Computer.Start();
                            return true;
                        case KeyCode.F9:
                            Computer.SingleStep();
                            Invalidate();
                            return true;
                        case KeyCode.F10:
                            Computer.StepOver();
                            return true;
                        case KeyCode.F11:
                            Computer.StepOut();
                            return true;
                        case KeyCode.F12:
                            if (Settings.Throttle)
                            {
                                Computer.Throttle = false;
                                MessageCallback("Throttle Off");
                            }
                            else
                            {
                                Computer.Throttle = true;
                                MessageCallback("Throttle On");
                            }
                            Settings.Throttle = Computer.Throttle;
                            return true;
                    }
                }
                if (!Key.Repeat)
                    return Computer.NotifyKeyboardChange(Key);
            }
            else if (Key.Pressed && Key.Alt)
            {
                if (!Key.Control && Key.Shift)
                {
                    // SHIFT-ALT
                    switch (Key.Key)
                    {
                        case KeyCode.N:
                            bool wasRunning = Computer.IsRunning;
                            Computer.Stop(true);
                            string path = Dialogs.GetSnapshotFile(Settings.LastSnapshotFile, true);
                            if (path.Length > 0)
                            {
                                Computer.SaveSnapshotFile(path);
                                Settings.LastSnapshotFile = path;
                            }
                            if (wasRunning)
                                Computer.Start();
                            return true;
                        case KeyCode.P:
                            // start the disassembly at the current PC location
                            Storage.SaveTextFile(System.IO.Path.Combine(ExecutablePath, "Disassembly.txt"), Computer.DumpDisassembly(true, true));
                            Dialogs.InformUser("Disassembly saved to \"Disassembly.txt\"");
                            return true;
                        case KeyCode.X:
                            OnUserCommand?.Invoke(UserCommand.Exit);
                            return true;
                        case KeyCode.End:
                            OnUserCommand?.Invoke(UserCommand.HardReset);
                            return true;
                    }
                }
                else if (!Key.Control && !Key.Shift)
                {
                    // ALT
                    switch (Key.Key)
                    {
                        case KeyCode.Return:
                            OnUserCommand?.Invoke(UserCommand.ToggleFullScreen);
                            return true;
                        case KeyCode.End:
                            Computer.Reset();
                            return true;
                        case KeyCode.A:
                            Settings.AutoStartOnReset = !Settings.AutoStartOnReset;
                            MessageCallback("Auto Start on Reset " + (Settings.AutoStartOnReset ? "On" : "Off"));
                            return true;
                        case KeyCode.B:
                            MakeFloppyFromCmdFile();
                            return true;
                        case KeyCode.C:
                            LoadCMDFile();
                            Invalidate();
                            return true;
                        case KeyCode.D:
                            CurrentMode = ViewMode.FloppyControllerView;
                            return true;
                        case KeyCode.E:
                            if (Log.TraceOn)
                            {
                                Log.TraceOn = false;
                                bool isRunning = Computer.IsRunning;
                                Computer.Stop(true);
                                Log.SaveTrace();
                                if (isRunning)
                                    Computer.Start();
                                MessageCallback("Trace run saved to 'trace.txt'");
                            }
                            else
                            {
                                Log.TraceOn = true;
                                MessageCallback("Collecting trace info...");
                            }
                            return true;
                        case KeyCode.F:
                            MakeAndSaveBlankFloppy(true);
                            return true;
                        case KeyCode.G:
                            OnUserCommand?.Invoke(UserCommand.GreenScreen);
                            MessageCallback("Screen color changed.");
                            return true;
                        case KeyCode.H:
                            Computer.HistoricDisassemblyMode = !Computer.HistoricDisassemblyMode;
                            MessageCallback(Computer.HistoricDisassemblyMode ? "Historic Disassembly Mode" : "Normal Disassembly Mode");
                            return true;
                        case KeyCode.I:
                            OnUserCommand?.Invoke(UserCommand.ShowInstructionSet);
                            return true;
                        case KeyCode.L:
                            Log.SaveLog();
                            MessageCallback("Log saved.");
                            return true;
                        case KeyCode.M:
                            CurrentMode = ViewMode.MemoryView;
                            return true;
                        case KeyCode.N:
                            string path = Dialogs.GetSnapshotFile(Settings.LastSnapshotFile, false);
                            if (path.Length > 0)
                            {
                                Computer.LoadSnapshotFile(path);
                                Settings.LastSnapshotFile = path;
                                MessageCallback("Snapshot Loaded");
                            }
                            return true;
                        case KeyCode.P:
                            Storage.SaveTextFile(System.IO.Path.Combine(ExecutablePath, "Disassembly.txt"), Computer.DumpDisassembly(true, false));
                            Dialogs.InformUser("Disassembly saved to \"Disassembly.txt\"");
                            return true;
                        case KeyCode.R:
                            CurrentMode = ViewMode.CpuView;
                            return true;
                        case KeyCode.S:
                            Settings.SoundOn = Computer.Sound.On = !Computer.Sound.On;
                            MessageCallback(Settings.SoundOn ? "Sound On" : "Sound Off");
                            return true;
                        case KeyCode.T:
                            Settings.DriveNoise = Computer.Sound.UseDriveNoise = !Computer.Sound.UseDriveNoise;
                            MessageCallback(Settings.DriveNoise ? "Drive noise on" : "Drive noise off");
                            return true;
                        case KeyCode.U:
                            MakeAndSaveBlankFloppy(false);
                            return true;
                        case KeyCode.Y:
                            LoadCMDFile(Computer.Assemble(), true);
                            return true;
                        case KeyCode.Z:
                            if (Computer.FloppyController.AnyDriveLoaded)
                                CurrentMode = ViewMode.DiskZapView;
                            return true;
                    }
                }
            }
            else if (Key.Pressed && Key.Control && !Key.Alt)
            {
                // Control pressed, ignores shift status
                switch (Key.Key)
                {
                    case KeyCode.Equals:
                        OnUserCommand?.Invoke(UserCommand.ZoomIn);
                        return true;
                    case KeyCode.Minus:
                        OnUserCommand?.Invoke(UserCommand.ZoomOut);
                        return true;
                }
            }
            return false;
        }
        public static byte[] GetViewData()
        {
            return views[CurrentMode].GetViewBytes();
        }
        protected abstract byte[] GetViewBytes();
        
        protected static byte[] PadScreen(byte[] Screen)
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
        protected static string Header(string HeaderText, string SubHeaderText = "")
        {
            return Center(HeaderText) +
                   Separator() +
                   (SubHeaderText.Length > 0 ? Center(SubHeaderText) + Format() : String.Empty);
        }
        protected static string Footer(string Text)
        {
            return Separator() +
                   Center(Text);
        }
        protected static string Center(string Input)
        {
            Debug.Assert(Input.Length <= ScreenDX.NUM_SCREEN_CHARS_X);

            return Input.PadLeft((ScreenDX.NUM_SCREEN_CHARS_X + Input.Length) / 2).PadRight(ScreenDX.NUM_SCREEN_CHARS_X);
        }

        protected static string Format(string Input)
        {
            return Format(Input, 0);
        }
        protected static string Format(string Input, int Indent)
        {
            Debug.Assert((Input.Length + Indent) <= ScreenDX.NUM_SCREEN_CHARS_X);

            return (new String(' ', Indent) + Input).PadRight(ScreenDX.NUM_SCREEN_CHARS_X);
        }
        protected static string Format(string[] Input, bool Indent)
        {
            switch (Input.Length)
            {
                case 0:
                    return Format();
                case 1:
                    if (Indent)
                        return View.Indent(Input[0]);
                    else
                        return Format(Input[0]);
                default:

                    int inputLength = Input.Sum(s => s.Length);

                    int extraSpace = ScreenDX.NUM_SCREEN_CHARS_X - inputLength - (Indent ? 2 * STANDARD_INDENT : 0);

                    Debug.Assert(extraSpace >= 0);

                    int numGaps = Input.Length - 1;

                    int minGapLength = (int)Math.Floor((decimal)extraSpace / (decimal)numGaps);
                    int maxGapLength = (int)Math.Ceiling((decimal)extraSpace / (decimal)numGaps);
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
        protected static string Format()
        {
            return Format("");
        }
        protected static string Indent(string Input)
        {
            return Format(Input, STANDARD_INDENT);
        }
        protected static string Separator(char Char = '=')
        {
            return new String(Char, ScreenDX.NUM_SCREEN_CHARS_X);
        }
        protected static void WriteToByteArray(byte[] Array, int Start, string Input)
        {
            for (int i = 0; i < Input.Length; i++)
                Array[i + Start] = (byte)Input[i];
        }
        protected static void WriteToByteArrayHex(byte[] Array, int Start, byte Input)
        {
            Array[Start]     = (Input >> 4)  .ToHexCharByte();
            Array[Start + 1] = (Input & 0x0F).ToHexCharByte();
        }

        private void LoadCMDFile(string Path = "", bool SuppressNormalInform = false)
        {
            if (String.IsNullOrWhiteSpace(Path))
                Path = Dialogs.GetCommandFile(Settings.LastCmdFile);

            if (Path.Length > 0)
            {
                if (Computer.LoadCMDFile(Path))
                {
                    if (!SuppressNormalInform)
                        Dialogs.InformUser("CMD File loaded.");
                    Settings.LastCmdFile = Path;
                }
                else
                {
                    Dialogs.AlertUser("CMD File load failed.");
                }
            }
        }
        private void MakeFloppyFromCmdFile()
        {
            string path = Dialogs.GetCmdFilePath(Settings.LastCmdFile);

            if (path.Length > 0)
            {
                if (Storage.MakeFloppyFromFile(path))
                    Settings.LastCmdFile = path;
            }
        }
        private void MakeAndSaveBlankFloppy(bool Formatted)
        {
            string path = Settings.DefaultFloppyDirectory;

            path = Storage.GetFloppyFilePath("Select floppy filename to create",
                                     path,
                                     Save: true,
                                     SelectFileInDialog: false,
                                     DskOnly: true);

            if (path.Length > 0)
            {
                var f = Storage.MakeBlankFloppy(Formatted);
                f.FilePath = path;
                if (Storage.SaveBinaryFile(path, f.Serialize(ForceDMK: true)))
                    Dialogs.InformUser("Created floppy OK.");
                else
                    Dialogs.AlertUser(string.Format("Failed to create floppy with filename {0}.", path),
                                      "Create floppy failed");
            }
        }
    }
}
