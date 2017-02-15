using System;
using System.IO;
using System.Windows.Forms;

namespace Sharp80
{
    public partial class MainForm : Form, IDXClient
    {
        public event MessageEventHandler Sizing;

        private ScreenDX screen;
        private UIController uic;
        private KeyboardDX keyboard;
        private bool resize = false;
        private Timer uiTimer;
        private Timer startTimer;

        private int previousClientHeight;

        public const uint SCREEN_REFRESH_RATE = 60; 
        private const int SCREEN_REFRESH_SLEEP = (int)(1000 / SCREEN_REFRESH_RATE);
        
        private const uint REPEAT_THRESHOLD = SCREEN_REFRESH_RATE / 2; // Half-second
        private const uint DISPLAY_MESSAGE_CYCLE_DURATION = SCREEN_REFRESH_RATE * 3 / 2;  // 1.5 seconds

        private bool IsActive { get; set; }

        public MainForm()
        {
            screen = new ScreenDX(Settings.AdvancedView,
                                  ViewMode.HelpView,
                                  DISPLAY_MESSAGE_CYCLE_DURATION,
                                  Settings.GreenScreen);
            
            InitializeComponent();

            uic = new UIController(screen, this, SCREEN_REFRESH_RATE);

            screen.UIC = uic;
            
            KeyPreview = true;
            Text = "Sharp80 - TRS-80 Model III Emulator";
            int h = (int)ScreenDX.WINDOWED_HEIGHT;
            int w = (int)(screen.AdvancedView ? ScreenDX.WINDOWED_WIDTH_ADVANCED : ScreenDX.WINDOWED_WIDTH_NORMAL);
            var scn = Screen.FromHandle(this.Handle);

            float defaultScale = 1f;

            while (w * defaultScale < scn.WorkingArea.Width / 2 && h * defaultScale < scn.WorkingArea.Height / 2)
            {
                defaultScale *= 2f;
            }

            ClientSize = new System.Drawing.Size((int)(w * defaultScale), (int)(h * defaultScale));

            uiTimer = new Timer() { Interval = SCREEN_REFRESH_SLEEP};
            uiTimer.Tick += UiTimerTick;
        }

        public bool IsMinimized { get { return this.WindowState == FormWindowState.Minimized; } }
        
        private void Form_Load(object sender, EventArgs e)
        {
            keyboard = new KeyboardDX();
            screen.Run(this);
            uic.Initialize();

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
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == MessageEventArgs.WM_SIZING)
                Sizing?.Invoke(this, new MessageEventArgs(m));

            base.WndProc(ref m);
        }
        private void Pause()
        {
            uic.Computer.Stop(WaitForStop: false);
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
            uic.Start();
            screen.ViewMode = ViewMode.NormalView;
            screen.StatusMessage = "Running...";
        }
        private bool leftShiftPressed = false;
        private bool rightShiftPressed = false;
        private bool leftControlPressed = false;
        private bool rightControlPressed = false;
        private bool leftAltPressed = false;
        private bool rightAltPressed = false;

        private SharpDX.DirectInput.Key repeatKey = SharpDX.DirectInput.Key.Unknown;
        private uint repeatKeyCount = 0;

        private bool IsShifted { get { return leftShiftPressed || rightShiftPressed; } }
        private bool IsControlPressed { get { return leftControlPressed || rightControlPressed; } }
        private bool IsAltPressed { get { return leftAltPressed || rightAltPressed; } }

        //protected override void OnKeyPress(KeyPressEventArgs e)
        //{
        //    e.Handled = true;
        //    base.OnKeyPress(e);
        //}

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

                if (this.IsActive)
                {
                    if (poll.Length > 0)
                    {
                        foreach (var k in poll)
                            ProcessKey(k.Key, k.IsPressed, k.IsReleased);
                    }
                    else if (repeatKey != SharpDX.DirectInput.Key.Unknown)
                    {
                        if (++repeatKeyCount > REPEAT_THRESHOLD)
                            ProcessKey(repeatKey, true, false);
                    }
                }
            }
        }

        private void ProcessKey(SharpDX.DirectInput.Key Key, bool IsPressed, bool IsReleased)
        {
            switch (Key)
            {
                case SharpDX.DirectInput.Key.LeftShift:
                    leftShiftPressed = IsPressed;
                    break;
                case SharpDX.DirectInput.Key.RightShift:
                    rightShiftPressed = IsPressed;
                    break;
                case SharpDX.DirectInput.Key.LeftControl:
                    leftControlPressed = IsPressed;
                    break;
                case SharpDX.DirectInput.Key.RightControl:
                    rightControlPressed = IsPressed;
                    break;
                case SharpDX.DirectInput.Key.LeftAlt:
                    leftAltPressed = IsPressed;
                    break;
                case SharpDX.DirectInput.Key.RightAlt:
                    rightAltPressed = IsPressed;
                    break;
            }
            if (IsReleased && Key == repeatKey)
            {
                repeatKey = SharpDX.DirectInput.Key.Unknown;
                repeatKeyCount = 0;
                return;
            }
            if (IsPressed)
            {
                if (IsAltPressed)
                {
                    switch (Key)
                    {
                        case SharpDX.DirectInput.Key.Return:
                            ToggleFullScreen();
                            break;
                        case SharpDX.DirectInput.Key.A:
                            Settings.AutoStartOnReset = !Settings.AutoStartOnReset;
                            screen.StatusMessage = "Auto Start on Reset " + (Settings.AutoStartOnReset ? "On" : "Off");
                            break;
                        case SharpDX.DirectInput.Key.Z:
                            MakeFloppyFromCmdFile();
                            break;
                        case SharpDX.DirectInput.Key.F:
                            MakeAndSaveBlankFloppy(true);
                            break;
                        case SharpDX.DirectInput.Key.U:
                            MakeAndSaveBlankFloppy(false);
                            break;
                        case SharpDX.DirectInput.Key.C:
                            LoadCMDFile();
                            break;
                        case SharpDX.DirectInput.Key.E:
                            if (Log.TraceOn)
                            {
                                Log.TraceOn = false;
                                bool isRunning = uic.Computer.IsRunning;
                                uic.Computer.Stop(true);
                                Log.SaveTrace();
                                if (isRunning)
                                    uic.Start();
                                screen.StatusMessage = "Trace run saved to 'trace.txt'";
                            }
                            else
                            {
                                Log.TraceOn = true;
                                screen.StatusMessage = "Collecting trace info...";
                            }
                            break;
                        case SharpDX.DirectInput.Key.H:
                            ToggleHistoricDisassembly();
                            break;
                        case SharpDX.DirectInput.Key.I:
                            ShowInstructionSetReport();
                            break;
                        case SharpDX.DirectInput.Key.L:
                            Log.SaveLog();
                            screen.StatusMessage = "Log saved.";
                            break;
                        case SharpDX.DirectInput.Key.M:
                            ToggleView(ViewMode.MemoryView);
                            break;
                        case SharpDX.DirectInput.Key.N:
                            DoSnapshot(IsShifted);
                            break;
                        case SharpDX.DirectInput.Key.R:
                            ToggleView(ViewMode.RegisterView);
                            break;
                        case SharpDX.DirectInput.Key.S:
                            ToggleSound();
                            break;
                        case SharpDX.DirectInput.Key.T:
                            ToggleDriveNoise();
                            break;
                        case SharpDX.DirectInput.Key.P:
                            DumpDissasembly();
                            break;
                        case SharpDX.DirectInput.Key.X:
                            if (IsShifted)
                                Exit();
                            break;
                        case SharpDX.DirectInput.Key.Y:
                            string path = uic.Computer.Assemble();
                            if (path.Length > 0)
                                uic.LoadCMDFile(path);
                            break;
                        case SharpDX.DirectInput.Key.G:
                            screen.GreenScreen = !screen.GreenScreen;
                            screen.StatusMessage = "Screen color changed.";
                            break;
#if CASSETTE
                        case SharpDX.DirectInput.Key.W:
                            rewindCassette();
                            break;
#endif
                        case SharpDX.DirectInput.Key.End:
                            Reset(IsShifted);
                            break;
                        
                    }
                }
                else // Alt not pressed
                {
                    switch (Key)
                    {
                        case SharpDX.DirectInput.Key.F1:
                            ToggleView(ViewMode.HelpView);
                            break;
                        case SharpDX.DirectInput.Key.F2:
                            ToggleView(ViewMode.OptionsView);
                            break;
                        case SharpDX.DirectInput.Key.F3:
                            ToggleView(ViewMode.DiskView);
                            break;
                        case SharpDX.DirectInput.Key.F4:
                            ToggleAdvancedView();
                            break;
                        case SharpDX.DirectInput.Key.F6:
                            ToggleView(ViewMode.JumpToView);
                            break;
                        case SharpDX.DirectInput.Key.F7:
                            ToggleView(ViewMode.SetBreakpointView);
                            break;
                        case SharpDX.DirectInput.Key.F8:
                            if (uic.Computer.IsRunning)
                                Pause();
                            else
                                Start();
                            break;
                        case SharpDX.DirectInput.Key.F9:
                            repeatKey = SharpDX.DirectInput.Key.F9;
                            uic.Computer.SingleStep();
                            break;
                        case SharpDX.DirectInput.Key.F10:
                            uic.Computer.StepOver();
                            break;
                        case SharpDX.DirectInput.Key.F11:
                            uic.Computer.StepOut();
                            break;
                        case SharpDX.DirectInput.Key.F12:
                            ToggleThrottle();
                            break;
                    }
                }
            }
            switch (screen.ViewMode)
            {
                case ViewMode.NormalView:
                    if (!IsAltPressed)
                        uic.Computer.NotifyKeyboardChange(Key, IsPressed);
                    break;
                case ViewMode.DiskZapView:
                case ViewMode.MemoryView:
                case ViewMode.RegisterView:
                    if (IsPressed)
                    {
                        repeatKey = Key;
                        screen.SendChar(Key, IsShifted);
                    }
                    break;
                case ViewMode.OptionsView:
                
                case ViewMode.HelpView:
                case ViewMode.SetBreakpointView:
                case ViewMode.JumpToView:
                    if (IsPressed)
                        screen.SendChar(Key, IsShifted);
                    break;
                case ViewMode.DiskView:
                    if (IsPressed)
                    {
                        switch (Key)
                        {
                            case SharpDX.DirectInput.Key.L:
                                if (screen.DiskViewFloppyNumber.HasValue)
                                    LoadFloppy(DriveNum: screen.DiskViewFloppyNumber.Value);
                                break;
                            case SharpDX.DirectInput.Key.E:
                                if (screen.DiskViewFloppyNumber.HasValue)
                                    EjectFloppy(DriveNum: screen.DiskViewFloppyNumber.Value);
                                break;
                            case SharpDX.DirectInput.Key.W:
                                if (screen.DiskViewFloppyNumber.HasValue)
                                    ToggleWriteProtection(DriveNum: screen.DiskViewFloppyNumber.Value);
                                break;
                            case SharpDX.DirectInput.Key.B:
                                if (screen.DiskViewFloppyNumber.HasValue) 
                                    MakeAndLoadBlankFloppy(Formatted: true, DriveNumber: screen.DiskViewFloppyNumber.Value);
                                break;
                            case SharpDX.DirectInput.Key.U:
                                if (screen.DiskViewFloppyNumber.HasValue)
                                    MakeAndLoadBlankFloppy(Formatted: false, DriveNumber: screen.DiskViewFloppyNumber.Value);
                                break;
                            default:
                                screen.SendChar(Key, IsShifted);
                                break;
                        }
                        screen.Invalidate();
                    }
                    break;
            }
        }

        private void Reset(bool HardReset)
        {
            if (HardReset)
            {
                SaveFloppies(out bool cancel);
                if (cancel)
                    return;
                else
                    uic.HardReset();
            }
            else
            {
                uic.Computer.Reset();
            }
            if (Settings.AutoStartOnReset)
                Start();

            screen.Invalidate();
        }
        private void ToggleFullScreen()
        {
            System.Diagnostics.Debug.WriteLine("Toggling full screen...");
            //screen.IsFullScreen = !screen.IsFullScreen;

            var fs = !screen.IsFullScreen;
            
            if (fs)
            {
                previousClientHeight = this.ClientSize.Height;
                
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                var parentScreen = Screen.FromHandle(this.Handle);
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
                    uic.Computer.SaveSnapshotFile(path);
                else
                    uic.Computer.LoadSnapshotFile(path);

                if (Settings.AutoStartOnReset)
                    Start();

                Settings.LastSnapshotFile = path;
            }
        }
        private void ToggleView(ViewMode Mode)
        {
            if (screen.ViewMode == Mode)
                screen.ViewMode = ViewMode.NormalView;
            else
                screen.ViewMode = Mode;
        }
        private void ToggleAdvancedView()
        {
            screen.AdvancedView = !screen.AdvancedView;
            screen.StatusMessage = screen.AdvancedView ? "Advanced View" : "Normal View";
            Settings.AdvancedView = screen.AdvancedView;
        }
        private void ToggleHistoricDisassembly()
        {
            uic.Computer.HistoricDisassemblyMode = !uic.Computer.HistoricDisassemblyMode;
            screen.StatusMessage = uic.Computer.HistoricDisassemblyMode ? "Historic Disassembly Mode" : "Normal Disassembly Mode";
        }
#if CASSETTE
        private void rewindCassette()
        {
            if (uic.RewindCassette())
                screen.ShowStatusMessage("Cassette Rewound.", DISPLAY_MESSAGE_CYCLE_DURATION);
            else
                screen.ShowStatusMessage("No Cassette Loaded.", DISPLAY_MESSAGE_CYCLE_DURATION);
        }
#endif
        private void ToggleThrottle()
        {
            if (Settings.Throttle)
            {
                uic.Computer.Clock.Throttle = false;
                screen.StatusMessage = "Throttle Off";
            }
            else
            {
                uic.Computer.Clock.Throttle = true;
                screen.StatusMessage = "Throttle On";
            }
            Settings.Throttle = uic.Computer.Clock.Throttle;
        }

        private void ToggleSound()
        {
            bool so = !uic.Computer.Sound.On;
            uic.Computer.Sound.On = so;
            screen.StatusMessage = so ? "Sound On" : "Sound Off";
            Settings.SoundOn = so;
        }
        private void ToggleDriveNoise()
        {
            bool dn = !uic.Computer.Sound.UseDriveNoise;
            uic.Computer.Sound.UseDriveNoise = dn;
            screen.StatusMessage = dn ? "Drive noise on" : "Drive noise off";
            Settings.DriveNoise = dn;
        }
        private void ShowInstructionSetReport()
        {
            TextForm st = new TextForm();

            st.ShowText(uic.Computer.GetInstructionSetReport(), "Z80 Instruction Set");
            st.Show();
            st.TextBox.SelectionLength = 0;
        }
        private void LoadFloppy(byte DriveNum)
        {
            string path = Storage.GetDefaultDriveFileName(DriveNum);
            bool selectFile = true;

            if (String.IsNullOrWhiteSpace(path))
            {
                path = Settings.DefaultFloppyDirectory;
                selectFile = false;
                if (String.IsNullOrWhiteSpace(path))
                {
                    path = Directory.GetParent(Application.ExecutablePath).FullName;
                    var p = Path.Combine(path, "Disks");
                    if (Directory.Exists(p))
                        path = p;
                }
            }

            path = GetFloppyFilePath(Prompt: string.Format("Select floppy file to load in drive {0}", DriveNum),
                                     DefaultPath: path,
                                     Save: false,
                                     SelectFileInDialog: selectFile,
                                     DskOnly: false);

            if (path.Length > 0)
            {
                if (SaveFloppyIfRequired(DriveNum) != DialogResult.Cancel)
                {
                    uic.Computer.LoadFloppy(DriveNum, path);
                    Settings.DefaultFloppyDirectory = Path.GetDirectoryName(path);
                    Storage.SaveDefaultDriveFileName(DriveNum, path);
                }
            }

            screen.Invalidate();
        }
        private void EjectFloppy(byte DriveNum)
        {
            if (SaveFloppyIfRequired(DriveNum) != DialogResult.Cancel)
                uic.Computer.EjectFloppy(DriveNum);
            
            screen.Invalidate();
        }
        private void ToggleWriteProtection(byte DriveNum)
        {
            bool? wp = uic.Computer.FloppyController.IsWriteProtected(DriveNum);

            if (wp.HasValue)
            {
                uic.Computer.FloppyController.SetIsWriteProtected(DriveNum, !wp.Value);
                screen.Invalidate();
            }
        }
        private string GetFloppyFilePath(string Prompt, string DefaultPath, bool Save, bool SelectFileInDialog, bool DskOnly)
        {
            string ext = DskOnly ? "dsk" : System.IO.Path.GetExtension(DefaultPath);

            if (string.IsNullOrWhiteSpace(ext))
                ext = "dsk";

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Prompt,
                                  Filter: DskOnly ? "TRS-80 DSK Files (*.dsk)|*.dsk|All Files (*.*)|*.*"
                                                  : "TRS-80 DSK Files (*.dsk;*.dmk;*.jv1;*.jv3)|*.dsk;*.dmk;*.jv1;*.jv3|All Files (*.*)|*.*",
                                  DefaultExtension: ext,
                                  SelectFileInDialog: SelectFileInDialog);
        }


        private DialogResult SaveFloppyIfRequired(byte DriveNum)
        {
            DialogResult dr;
            // Return true to cancel save, false if OK to save
            if (uic.Computer.FloppyController.DiskHasChanged(DriveNum) ?? false)
                dr = MessageBox.Show(string.Format("Drive {0} has changed. Save it?", DriveNum),
                                                   "Save Changed Drive?",
                                                   MessageBoxButtons.YesNoCancel,
                                                   MessageBoxIcon.Question);
            else
                dr = DialogResult.No;

            if (dr == DialogResult.Yes)
            {
                if (string.IsNullOrWhiteSpace(uic.Computer.FloppyController.FloppyFilePath(DriveNum)))
                {
                    var path = GetFloppyFilePath("Choose path to save floppy", Settings.DefaultFloppyDirectory, true, false, true);
                    if (string.IsNullOrWhiteSpace(path))
                        return DialogResult.Cancel;
                    else
                    {
                        uic.Computer.FloppyController.GetFloppy(DriveNum).FilePath = path;
                        Storage.SaveDefaultDriveFileName(DriveNum, path);
                    }
                }
                uic.Computer.FloppyController.SaveFloppy(DriveNum);
            }
            return dr;
        }
        private void MakeFloppyFromCmdFile()
        {
            string path = GetTRS80File(Settings.LastCmdFile);

            if (path.Length > 0)
            {
                if (uic.MakeFloppyFromFile(path))
                    Settings.LastCmdFile = path;
            }
        }
        private void MakeAndSaveBlankFloppy(bool Formatted)
        {
            string path = Settings.DefaultFloppyDirectory;

            path = GetFloppyFilePath("Select floppy filename to create",
                                     path,
                                     Save: true,
                                     SelectFileInDialog: false,
                                     DskOnly: true);

            if (path.Length > 0)
            {
                var f = Storage.MakeBlankFloppy(Formatted);
                if (!f.IsEmpty)
                {
                    if (Storage.SaveBinaryFile(path, f.Serialize(ForceDMK: true)))
                        MessageBox.Show("Created floppy OK.",
                                        "Created floppy",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Information);
                    else
                        MessageBox.Show(string.Format("Failed to create floppy with filename {0}.", path),
                                        "Create floppy failed",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                }
            }
        }

        private void MakeAndLoadBlankFloppy(bool Formatted, byte DriveNumber)
        {
            if (SaveFloppyIfRequired(DriveNum: DriveNumber) != DialogResult.Cancel)
            {
                var f = Storage.MakeBlankFloppy(Formatted);

                if (!f.IsEmpty)
                    uic.Computer.LoadFloppy(DriveNumber, f);
            }
        }
        private void LoadCMDFile()
        {
            string path = GetCommandFile(Settings.LastCmdFile);

            if (path.Length > 0)
                if (uic.LoadCMDFile(path))
                    Settings.LastCmdFile = path;
        }
        private void Exit()
        {
            Settings.Save();
            this.Dispose();
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
            string dir = DefaultPath.Length > 0 ? System.IO.Path.GetDirectoryName(DefaultPath) :
                                                  System.IO.Path.GetDirectoryName(Application.ExecutablePath);

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
                if (Save || System.IO.File.Exists(path))
                    return path;

            return string.Empty;

        }
        private void DumpDissasembly()
        {
            Storage.SaveTextFile(Lib.GetAppPath() + "Disassembly_Dump.txt", uic.Computer.DumpDisassembly(true));
            MessageBox.Show("Disassembly saved to 'Disassembly_Dump.txt'", "Disassembly Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveFloppies(out bool cancel);

            if (cancel)
            {
                e.Cancel = true;
            }
            else
            {
                Log.SaveLog();
            }
        }

        private void SaveFloppies(out bool Cancel)
        {
            // returns true on user cancel

            for (byte b = 0; b < 4; b++)
            {
                Storage.SaveDefaultDriveFileName(b, uic.Computer.FloppyController.FloppyFilePath(b));

                if (SaveFloppyIfRequired(b) == DialogResult.Cancel)
                {
                    Cancel = true;
                    return;
                }
            }
            Cancel = false;
        }

        private void Form_Activated(object sender, EventArgs e)
        {
            repeatKey = SharpDX.DirectInput.Key.Unknown;

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
            uic.ResetKeyboard();
        }
    }
}