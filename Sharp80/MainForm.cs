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
        private Timer uiTimer;
        private int resizing = 0;

        private int previousClientHeight;

        public const uint SCREEN_REFRESH_RATE = 60;
        private const int SCREEN_REFRESH_SLEEP = (int)(1000 / SCREEN_REFRESH_RATE);

        private const uint REPEAT_THRESHOLD = SCREEN_REFRESH_RATE / 2;                    // Half-second (30 cycles)
        private const uint DISPLAY_MESSAGE_CYCLE_DURATION = SCREEN_REFRESH_RATE * 3 / 2;  // 1.5 seconds

        private bool IsActive { get; set; }

        public MainForm()
        {
            KeyPreview = true;
            Text = "Sharp 80 - TRS-80 Model III Emulator";

            Dialogs.Initialize(this);

            screen = new ScreenDX(Settings.AdvancedView,
                                  ViewMode.Help,
                                  DISPLAY_MESSAGE_CYCLE_DURATION,
                                  Settings.GreenScreen);

            InitializeComponent();
            SetupClientArea();

            uiTimer = new Timer() { Interval = SCREEN_REFRESH_SLEEP };
            uiTimer.Tick += UiTimerTick;
        }

        private void SetupClientArea()
        {
            int h = (int)ScreenDX.WINDOWED_HEIGHT;
            int w = (int)(screen.AdvancedView ? ScreenDX.WINDOWED_WIDTH_ADVANCED : ScreenDX.WINDOWED_WIDTH_NORMAL);
            var scn = Screen.FromHandle(Handle);

            float defaultScale = 1f;

            while (w * defaultScale < scn.WorkingArea.Width / 2 && h * defaultScale < scn.WorkingArea.Height / 2)
            {
                defaultScale *= 2f;
            }

            ClientSize = new System.Drawing.Size((int)(w * defaultScale), (int)(h * defaultScale));
        }

        public bool IsMinimized { get { return WindowState == FormWindowState.Minimized; } }

        private void Form_Load(object sender, EventArgs e)
        {
            ResizeBegin += (o, ee) => { resizing++; }; 
            ResizeEnd   += (o, ee) => { resizing--; }; 

            keyboard = new KeyboardDX();
            HardReset();
            View.OnUserCommand += OnUserCommand;
            screen.Initialize(this, computer);

            if (Settings.AutoStartOnReset)
            {
                var startTimer = new Timer() { Interval = 500 };
                startTimer.Tick += (s, ee) => { startTimer.Stop(); startTimer.Dispose(); computer.Start(); };
                startTimer.Start();
            }
            
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
                case UserCommand.ZoomIn:
                    Zoom(true);
                    break;
                case UserCommand.ZoomOut:
                    Zoom(false);
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
                if (!IsMinimized && resizing == 0)
                    screen.Render();
                
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
            }
            if (View.ProcessKey(k))
                return;
        }
        private void SyncKeyboard()
        {
            repeatKey = KeyCode.None;

            keyboard.UpdateModifierKeys(out leftShiftPressed,
                                        out rightShiftPressed,
                                        out leftAltPressed,
                                        out rightAltPressed,
                                        out leftControlPressed,
                                        out rightControlPressed);

            computer.ResetKeyboard();

            if (View.CurrentMode == ViewMode.Normal)
            {
                ProcessKey(new KeyState(KeyCode.LeftShift,    false, false, false, leftShiftPressed,    !leftShiftPressed));
                ProcessKey(new KeyState(KeyCode.RightShift,   false, false, false, rightShiftPressed,   !rightShiftPressed));
            }
        }
        private void ToggleFullScreen(bool AdjustClientSize = true)
        {
            var fs = !screen.IsFullScreen;
            resizing++;

            Settings.FullScreen = screen.IsFullScreen = fs;

            if (fs)
            {
                previousClientHeight = ClientSize.Height;
                if (AdjustClientSize)
                    SetWindowStyle();
            }
            else
            {
                if (AdjustClientSize)
                {
                    SetWindowStyle();
                    ClientSize = new System.Drawing.Size((int)(previousClientHeight * (screen.AdvancedView ? ScreenDX.WINDOWED_ASPECT_RATIO_ADVANCED : ScreenDX.WINDOWED_ASPECT_RATIO_NORMAL)),
                                                               previousClientHeight);
                }
            }

            resizing--;
        }
        private void SetWindowStyle()
        {
            resizing++;

            if (screen.IsFullScreen)
            {
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
            }

            resizing--;
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
            if (Storage.SaveFloppies(computer))
            {
                Settings.Save();
                Log.Save();
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
            SyncKeyboard();

            IsActive = true;
        }

        private void Form_Deactivate(object sender, EventArgs e)
        {
            IsActive = false;
            computer.ResetKeyboard();
        }
        private void Zoom(bool In)
        {
            if (IsMinimized)
                return;

            if (screen.IsFullScreen && In)
                return;

            float aspectRatio = (screen.AdvancedView ? ScreenDX.WINDOWED_WIDTH_ADVANCED : ScreenDX.WINDOWED_WIDTH_NORMAL) / ScreenDX.WINDOWED_HEIGHT;

            var scn = Screen.FromHandle(Handle);

            int curW, curH, curX, curY, scrW, scrH;

            scrW = scn.WorkingArea.Width;
            scrH = scn.WorkingArea.Height;

            if (screen.IsFullScreen)
            {
                curW = scrW;
                curH = scrH;
                curX = curY = 0;
            }
            else
            {
                curW = ClientSize.Width;
                curH = ClientSize.Height;
                curX = Location.X;
                curY = Location.Y;
            }
            float zoom = In ? 1.2f : 1f/1.2f;

            int newH = (int)(curH * zoom);

            if (newH > ScreenDX.WINDOWED_HEIGHT * 0.88f && newH < ScreenDX.WINDOWED_HEIGHT * 1.102f)
                newH = (int)ScreenDX.WINDOWED_HEIGHT;

            if (newH > 2 * ScreenDX.WINDOWED_HEIGHT * 0.88f && newH < 2 * ScreenDX.WINDOWED_HEIGHT * 1.102f)
                newH = 2 * (int)ScreenDX.WINDOWED_HEIGHT;

            int newW = (int)(newH * aspectRatio);

            if (newW > scrW || newH > scrH)
            {
                if (screen.IsFullScreen)
                    return;
                else
                    ToggleFullScreen(false);
            }
            else
            {
                if (screen.IsFullScreen)
                    ToggleFullScreen(false);
            }
            int xOffset = (newW - curW) / 2;
            int yOffset = (newH - curH) / 2;

            int newX = curX - xOffset;
            int newY = curY - yOffset;

            newX = Math.Min(Math.Max(0, newX), scrW - newW);
            newY = Math.Min(Math.Max(0, newY), scrH - newH);

            resizing++;

            ClientSize = new System.Drawing.Size(newW, newH);
            Location = new System.Drawing.Point(newX, newY);
            SetWindowStyle();

            resizing--;

            previousClientHeight = newH;
        }
        private void HardReset()
        {
            if (computer != null)
            {
                if (!Storage.SaveFloppies(computer))
                    return;
                computer.Dispose();
            }
            computer = new Computer(this, screen, SCREEN_REFRESH_RATE, Settings.NormalSpeed)
            {
                SoundOn =      Settings.SoundOn,
                DriveNoise =   Settings.DriveNoise,
                BreakPoint =   Settings.Breakpoint,
                BreakPointOn = Settings.BreakpointOn
            };
            computer.StartupLoadFloppies();
            screen.Initialize(this, computer);

            View.Initialize(computer, (msg) => screen.StatusMessage = msg);
        }
    }
}