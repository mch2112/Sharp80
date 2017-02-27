/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
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

        public bool IsMinimized { get { return WindowState == FormWindowState.Minimized; } }

        private void Form_Load(object sender, EventArgs e)
        {
            keyboard = new KeyboardDX();

            HardReset();
            
            View.OnUserCommand += OnUserCommand;

            screen.Initialize(this);

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

            if (Settings.FullScreen)
                ToggleFullScreen();
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
                case UserCommand.HardReset:
                    HardReset();
                    break;
                case UserCommand.Exit:
                    Close();
                    break;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == MessageEventArgs.WM_SIZING)
                Sizing?.Invoke(this, new MessageEventArgs(m));

            base.WndProc(ref m);
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

        private bool IsShifted        { get { return leftShiftPressed   || rightShiftPressed; } }
        private bool IsControlPressed { get { return leftControlPressed || rightControlPressed; } }
        private bool IsAltPressed     { get { return leftAltPressed     || rightAltPressed; } }

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
                            ProcessRepeatKey(repeatKey);
                    }
                }
            }
        }

        private void ProcessKey(SharpDX.DirectInput.KeyboardUpdate Key)
        {
            ProcessKey(new KeyState((KeyCode)Key.Key, IsShifted, IsControlPressed, IsAltPressed, Key.IsPressed, Key.IsReleased));
        }
        private void ProcessRepeatKey(KeyCode Key)
        {
            ProcessKey(new KeyState(Key, IsShifted, IsControlPressed, IsAltPressed, true, false, true));
        }
        private void ProcessKey(KeyState k)
        {
            switch (k.Key)
            {
                case KeyCode.LeftShift:    leftShiftPressed =    k.Pressed; break;
                case KeyCode.RightShift:   rightShiftPressed =   k.Pressed; break;
                case KeyCode.LeftControl:  leftControlPressed =  k.Pressed; break;
                case KeyCode.RightControl: rightControlPressed = k.Pressed; break;
                case KeyCode.LeftAlt:      leftAltPressed =      k.Pressed; break;
                case KeyCode.RightAlt:     rightAltPressed =     k.Pressed; break;
            }
            if (k.Pressed)
            {
                switch (k.Key)
                {
                    case KeyCode.Up:
                    case KeyCode.Down:
                    case KeyCode.Left:
                    case KeyCode.Right:
                    case KeyCode.PageUp:
                    case KeyCode.PageDown:
                    case KeyCode.F9:
                        repeatKey = k.Key;
                        break;
                }
            }
            else if (k.Key == repeatKey)
            {
                repeatKey = KeyCode.None;
                repeatKeyCount = 0;
                return;
            }
            if (View.ProcessKey(k))
                return;
        }

        private void ToggleFullScreen()
        {
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
            Settings.FullScreen = screen.IsFullScreen = fs;
        }
        private void ShowInstructionSetReport()
        {
            TextForm st = new TextForm();

            st.ShowText(computer.GetInstructionSetReport(), "Z80 Instruction Set");
            st.Show();
            st.TextBox.SelectionLength = 0;
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

            keyboard.UpdateModifierKeys(out leftShiftPressed,
                                        out rightShiftPressed,
                                        out leftAltPressed,
                                        out rightAltPressed,
                                        out leftControlPressed,
                                        out rightControlPressed);

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