/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace Sharp80
{
    public partial class MainForm : Form, IDXClient
    {
        public event MessageEventHandler Sizing;

        private Computer computer;
        private ScreenDX screen;
        private KeyboardDX keyboard;
        private bool resize = false;
        private Timer uiTimer;
        private Timer startTimer;

        private int previousClientHeight;

        public const uint SCREEN_REFRESH_RATE = 60;
        private const int SCREEN_REFRESH_SLEEP = (int)(1000 / SCREEN_REFRESH_RATE);

        private const uint REPEAT_THRESHOLD = SCREEN_REFRESH_RATE / 2;                    // Half-second (30 cycles)
        private const uint DISPLAY_MESSAGE_CYCLE_DURATION = SCREEN_REFRESH_RATE * 3 / 2;  // 1.5 seconds

        private bool IsActive { get; set; }

        public MainForm()
        {
            Dialogs.Initialize(this);

            screen = new ScreenDX(Settings.AdvancedView,
                                  ViewMode.HelpView,
                                  DISPLAY_MESSAGE_CYCLE_DURATION,
                                  Settings.GreenScreen);

            InitializeComponent();

            KeyPreview = true;
            Text = "Sharp80 - TRS-80 Model III Emulator";
            int h = (int)ScreenDX.WINDOWED_HEIGHT;
            int w = (int)(screen.AdvancedView ? ScreenDX.WINDOWED_WIDTH_ADVANCED : ScreenDX.WINDOWED_WIDTH_NORMAL);
            var scn = System.Windows.Forms.Screen.FromHandle(Handle);

            float defaultScale = 1f;

            while (w * defaultScale < scn.WorkingArea.Width / 2 && h * defaultScale < scn.WorkingArea.Height / 2)
            {
                defaultScale *= 2f;
            }

            ClientSize = new System.Drawing.Size((int)(w * defaultScale), (int)(h * defaultScale));

            uiTimer = new Timer() { Interval = SCREEN_REFRESH_SLEEP };
            uiTimer.Tick += UiTimerTick;
        }

        public bool IsMinimized { get { return this.WindowState == FormWindowState.Minimized; } }

        private void Form_Load(object sender, EventArgs e)
        {
            keyboard = new KeyboardDX();

            HardReset();
            
            View.OnUserCommand += OnUserCommand;

            screen.Initialize(this);

#if CASSETTE
            uic.LoadCassette(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "startrek.cas"));
#endif
            if (Settings.AutoStartOnReset)
            {
                startTimer = new Timer() { Interval = 500 };
                startTimer.Tick += (s, ee) => { Start(); };
                startTimer.Start();
            }

            ClientSizeChanged += (s, ee) =>
            {
                resize = true;
            };

            uiTimer.Start();
        }

        private void OnUserCommand(UserCommand Command)
        {
            switch(Command)
            {
                case UserCommand.ShowInstructionSet:
                    ShowInstructionSetReport();
                    break;
                case UserCommand.ToggleFullScreen:
                    ToggleFullScreen();
                    break;
                case UserCommand.Exit:
                    Exit();
                    break;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == MessageEventArgs.WM_SIZING)
                Sizing?.Invoke(this, new MessageEventArgs(m));

            base.WndProc(ref m);
        }
        private void Pause()
        {
            computer.Stop(WaitForStop: false);
            screen.StatusMessage = "Paused";
        }
        private void Start()
        {
            if (startTimer != null)
            {
                startTimer.Stop();
                startTimer.Dispose();
                startTimer = null;
            }
            computer.Start();
        }
        private bool leftShiftPressed = false;
        private bool rightShiftPressed = false;
        private bool leftControlPressed = false;
        private bool rightControlPressed = false;
        private bool leftAltPressed = false;
        private bool rightAltPressed = false;

        private KeyCode repeatKey = KeyCode.None;
        private uint repeatKeyCount = 0;

        private bool IsShifted { get { return leftShiftPressed || rightShiftPressed; } }
        private bool IsControlPressed { get { return leftControlPressed || rightControlPressed; } }
        private bool IsAltPressed { get { return leftAltPressed || rightAltPressed; } }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Prevent stupid ding noise

            if (e.KeyCode < Keys.F1 || e.KeyCode > Keys.F19 || (!e.Alt && !e.Control))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
            }
        }

        private void UiTimerTick(object Sender, EventArgs e)
        {
            if (!Disposing)
            {
                // Render Screen

                if (WindowState != FormWindowState.Minimized)
                {
                    if (resize)
                    {
                        try
                        {
                            resize = false;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.ToString());
                        }
                    }
                    screen.Render();
                }

                // Handle Keyboard Events

                var poll = keyboard.Poll();

                if (IsActive)
                {
                    if (poll.Length > 0)
                    {
                        foreach (var k in poll)
                            ProcessKey(k);
                    }
                    else if (repeatKey != KeyCode.None)
                    {
                        if (++repeatKeyCount > REPEAT_THRESHOLD)
                            ProcessKey(repeatKey);
                    }
                }
            }
        }

        private void ProcessKey(SharpDX.DirectInput.KeyboardUpdate Key)
        {
            ProcessKey(new KeyState((KeyCode)Key.Key, IsShifted, IsControlPressed, IsAltPressed, Key.IsPressed, Key.IsReleased));
        }
        private void ProcessKey(KeyCode Key)
        {
            ProcessKey(new KeyState(Key, IsShifted, IsControlPressed, IsAltPressed, true, false));
        }
        private void ProcessKey(KeyState k)
        {
            switch (k.Key)
            {
                case KeyCode.LeftShift:
                    leftShiftPressed = k.Pressed;
                    break;
                case KeyCode.RightShift:
                    rightShiftPressed = k.Pressed;
                    break;
                case KeyCode.LeftControl:
                    leftControlPressed = k.Pressed;
                    break;
                case KeyCode.RightControl:
                    rightControlPressed = k.Pressed;
                    break;
                case KeyCode.LeftAlt:
                    leftAltPressed = k.Pressed;
                    break;
                case KeyCode.RightAlt:
                    rightAltPressed = k.Pressed;
                    break;
            }

            if (View.ProcessKey(k))
                return;

            if (k.Released && k.Key == repeatKey)
            {
                repeatKey = KeyCode.None;
                repeatKeyCount = 0;
                return;
            }
            if (k.Pressed)
            {
                if (IsAltPressed)
                {
                    switch (k.Key)
                    {
                        case KeyCode.Z:
                            MakeFloppyFromCmdFile();
                            return;
                        case KeyCode.F:
                            MakeAndSaveBlankFloppy(true);
                            return;
                        case KeyCode.U:
                            MakeAndSaveBlankFloppy(false);
                            return;
                        case KeyCode.C:
                            LoadCMDFile();
                            return;
                        case KeyCode.E:
                            if (Log.TraceOn)
                            {
                                Log.TraceOn = false;
                                bool isRunning = computer.IsRunning;
                                computer.Stop(true);
                                Log.SaveTrace();
                                if (isRunning)
                                    computer.Start();
                                screen.StatusMessage = "Trace run saved to 'trace.txt'";
                            }
                            else
                            {
                                Log.TraceOn = true;
                                screen.StatusMessage = "Collecting trace info...";
                            }
                            return;
                        case KeyCode.D:
                            //ToggleView(ViewMode.FloppyControllerView);
                            return;
                        case KeyCode.L:
                            Log.SaveLog();
                            screen.StatusMessage = "Log saved.";
                            return;
                        case KeyCode.N:
                            DoSnapshot(IsShifted);
                            return;
                        case KeyCode.R:
                            //ToggleView(ViewMode.RegisterView);
                            return;
                        case KeyCode.P:
                            DumpDissasembly();
                            return;
                        case KeyCode.Y:
                            string path = computer.Assemble();
                            LoadCMDFile(path, true);
                            return;
#if CASSETTE
                        case KeyCode.W:
                            rewindCassette();
                            return;
#endif
                    }
                }
                else // Alt not pressed
                {
                //    switch (k.Key)
                //    {
                //        case KeyCode.F8:
                //            if (uic.Computer.IsRunning)
                //                Pause();
                //            else
                //                Start();
                //            break;
                //        case KeyCode.F9:
                //            repeatKey = KeyCode.F9;
                //            uic.Computer.SingleStep();
                //            screen.Invalidate();
                //            break;
                //        case KeyCode.F10:
                //            uic.Computer.StepOver();
                //            break;
                //        case KeyCode.F11:
                //            uic.Computer.StepOut();
                //            break;
                //        case KeyCode.F12:
                //            ToggleThrottle();
                //            break;
                //    }
                }
            }
            //switch (screen.ViewMode)
            //{
            //    case ViewMode.NormalView:
            //        if (!IsAltPressed)
            //            uic.Computer.NotifyKeyboardChange(Key, IsPressed);
            //        break;
            //    case ViewMode.DiskZapView:
            //    case ViewMode.MemoryView:
            //    case ViewMode.FloppyControllerView:
            //        if (!IsAltPressed && !IsControlPressed)
            //        {
            //            if (IsPressed)
            //            {
            //                repeatKey = Key;
            //                if (!screen.SendChar(Key, IsShifted))
            //                    uic.Computer.NotifyKeyboardChange(Key, true);
            //            }
            //            else
            //            {
            //                uic.Computer.NotifyKeyboardChange(Key, false);
            //            }
            //        }

            //        break;
            //    case ViewMode.OptionsView:
            //    case ViewMode.HelpView:
            //    case ViewMode.SetBreakpointView:
            //    case ViewMode.JumpToView:
            //    case ViewMode.RegisterView:
            //        if (IsPressed && !IsAltPressed && !IsControlPressed)
            //            screen.SendChar(Key, IsShifted);
            //        break;
            //    case ViewMode.DiskView:
            //        if (IsPressed)
            //        {
            //            switch (Key)
            //            {
            //                //case SharpDX.DirectInput.Key.L:
            //                //    if (screen.DiskViewFloppyNumber.HasValue)
            //                //        LoadFloppy(DriveNum: screen.DiskViewFloppyNumber.Value);
            //                //    break;

            //                case SharpDX.DirectInput.Key.W:
            //                    if (screen.DiskViewFloppyNumber.HasValue)
            //                        ToggleWriteProtection(DriveNum: screen.DiskViewFloppyNumber.Value);
            //                    break;
            //                case SharpDX.DirectInput.Key.T:
            //                    if (screen.DiskViewFloppyNumber.HasValue)
            //                        uic.Computer.LoadTrsDosFloppy(screen.DiskViewFloppyNumber.Value);
            //                    break;
            //                default:
            //                    screen.SendChar(Key, IsShifted);
            //                    break;
            //            }
            //            screen.Invalidate();
            //        }
            //        break;
            //}
        }

        private void ToggleFullScreen()
        {
            System.Diagnostics.Debug.WriteLine("Toggling full screen...");

            var fs = !screen.IsFullScreen;

            if (fs)
            {
                previousClientHeight = ClientSize.Height;

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                var parentScreen = System.Windows.Forms.Screen.FromHandle(Handle);
                ClientSize = parentScreen.Bounds.Size;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                ClientSize = new System.Drawing.Size((int)(previousClientHeight * (screen.AdvancedView ? ScreenDX.WINDOWED_ASPECT_RATIO_ADVANCED : ScreenDX.WINDOWED_ASPECT_RATIO_NORMAL)),
                                                           previousClientHeight);
            }
            screen.IsFullScreen = fs;
        }
        private void DoSnapshot(bool Save)
        {
            string path = GetSnapshotFile(Settings.LastSnapshotFile, Save);

            if (path.Length > 0)
            {
                if (Save)
                    computer.SaveSnapshotFile(path);
                else
                    computer.LoadSnapshotFile(path);

                Settings.LastSnapshotFile = path;
            }
        }
        private void ShowInstructionSetReport()
        {
            TextForm st = new TextForm();

            st.ShowText(computer.GetInstructionSetReport(), "Z80 Instruction Set");
            st.Show();
            st.TextBox.SelectionLength = 0;
        }

        // returns true if it's ok to continue the pending operation
        private void MakeFloppyFromCmdFile()
        {
            string path = GetTRS80File(Settings.LastCmdFile);

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

        private void LoadCMDFile(string Path = "", bool SuppressNormalInform = false)
        {
            if (String.IsNullOrWhiteSpace(Path))
                Path = GetCommandFile(Settings.LastCmdFile);

            if (Path.Length > 0)
            {
                if (computer.LoadCMDFile(Path))
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
        private void Exit()
        {
            Close();
        }
        private string GetTRS80File(string DefaultPath)
        {
            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select File",
                                  Filter: "TRS-80 Files (*.cmd; *.bas; *.txt)|*.cmd;*.bas;*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        private string GetCommandFile(string DefaultPath)
        {
            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select CMD File",
                                  Filter: "TRS-80 CMD Files (*.cmd)|*.cmd|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        private string GetSnapshotFile(string DefaultPath, bool Save)
        {
            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Snapshot File" : "Load Snapshot File",
                                  Filter: "TRS-80 Snapshot Files (*.snp)|*.snp|All Files (*.*)|*.*",
                                  DefaultExtension: "snp",
                                  SelectFileInDialog: true);
        }
        private string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog)
        {
            string dir = DefaultPath.Length > 0 ? Path.GetDirectoryName(DefaultPath) :
                                                  Path.GetDirectoryName(Application.ExecutablePath);

            FileDialog dialog;

            if (Save)
            {
                var saveDialog = new SaveFileDialog();

                dialog = saveDialog;
                saveDialog.OverwritePrompt = true;
            }
            else
            {
                var openDialog = new OpenFileDialog();
                dialog = openDialog;

                openDialog.Multiselect = false;
                openDialog.CheckPathExists = true;
            }
            dialog.Title = Title;
            dialog.FileName = (SelectFileInDialog && DefaultPath.Length > 0) ? System.IO.Path.GetFileName(DefaultPath) : string.Empty;
            dialog.InitialDirectory = dir;
            dialog.ValidateNames = true;
            dialog.Filter = Filter;
            dialog.AddExtension = true;
            dialog.DefaultExt = DefaultExtension;

            DialogResult dr = dialog.ShowDialog(this);

            string path = dialog.FileName;

            if (dr == DialogResult.OK && path.Length > 0)
                if (Save || File.Exists(path))
                    return path;

            return string.Empty;

        }
        private void DumpDissasembly()
        {
            Storage.SaveTextFile(Path.Combine(Lib.GetAppPath(), "Disassembly_Dump.txt"), computer.DumpDisassembly(true));
            Dialogs.InformUser("Disassembly saved to \"Disassembly_Dump.txt\"");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Storage.SaveFloppies(computer.FloppyController))
            {
                Settings.Save();
                Log.SaveLog();
                Dispose();
            }
            else
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
        
        private void Form_Activated(object sender, EventArgs e)
        {
            repeatKey = KeyCode.None;

            leftShiftPressed    = keyboard.IsPressed(SharpDX.DirectInput.Key.LeftShift);
            rightShiftPressed   = keyboard.IsPressed(SharpDX.DirectInput.Key.RightShift);
            leftAltPressed      = keyboard.IsPressed(SharpDX.DirectInput.Key.LeftAlt);
            rightAltPressed     = keyboard.IsPressed(SharpDX.DirectInput.Key.RightAlt);
            leftControlPressed  = keyboard.IsPressed(SharpDX.DirectInput.Key.LeftControl);
            rightControlPressed = keyboard.IsPressed(SharpDX.DirectInput.Key.RightControl);

            IsActive = true;
        }

        private void Form_Deactivate(object sender, EventArgs e)
        {
            IsActive = false;
            computer.ResetKeyboard();
        }
        private void HardReset()
        {
            if (computer != null)
            {
                if (!Storage.SaveFloppies(computer.FloppyController))
                    return;
                computer.Dispose();
            }
            computer = new Computer(this, screen, SCREEN_REFRESH_RATE, Settings.Throttle);
            computer.StartupLoadFloppies();
            computer.Sound.UseDriveNoise = Settings.DriveNoise;
            computer.Processor.BreakPoint = Settings.Breakpoint;
            computer.Processor.BreakPointOn = Settings.BreakpointOn;

            View.Initialize(computer, Application.ExecutablePath, (msg) => screen.StatusMessage = msg);
        }
    }
}