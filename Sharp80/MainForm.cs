/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Windows.Forms;

namespace Sharp80
{
    public partial class MainForm : Form, IAppWindow
    {
        public event MessageEventHandler Sizing;

        private Computer computer;
        private IScreen screen;
        private IKeyboard keyboard;
        private Timer uiTimer;
        private int resizing = 0;

        private int previousClientHeight;

        public const uint SCREEN_REFRESH_RATE = 60;
        private const int SCREEN_REFRESH_SLEEP = (int)(1000 / SCREEN_REFRESH_RATE);

        private const uint REPEAT_THRESHOLD = SCREEN_REFRESH_RATE / 2;                    // Half-second (30 cycles)
        private const uint DISPLAY_MESSAGE_CYCLE_DURATION = SCREEN_REFRESH_RATE;  // 1 seconds

        private bool IsActive { get; set; }

        public MainForm()
        {
            KeyPreview = true;
            Text = "Sharp 80 - TRS-80 Model III Emulator";

            Dialogs.Initialize(this);

            screen = new ScreenDX(Settings.AdvancedView,
                                  DISPLAY_MESSAGE_CYCLE_DURATION,
                                  Settings.GreenScreen);

            InitializeComponent();
            SetupClientArea();

            uiTimer = new Timer() { Interval = SCREEN_REFRESH_SLEEP };
            uiTimer.Tick += UiTimerTick;
        }

        private void SetupClientArea()
        {
            var scn = Screen.FromHandle(Handle);

            int clientExtraW = Width - ClientSize.Width;
            int clientExtraH = Height - ClientSize.Height;

            int sw = scn.WorkingArea.Width;
            int sh = scn.WorkingArea.Height;

            int w = Settings.WindowWidth;
            int h = Settings.WindowHeight;

            if (w <= 0 || h <= 0 || w + clientExtraW > sw || h + clientExtraH > sh)
            {
                w = (int)(screen.AdvancedView ? ScreenMetrics.WINDOWED_WIDTH_ADVANCED : ScreenMetrics.WINDOWED_WIDTH_NORMAL);
                h = (int)ScreenMetrics.WINDOWED_HEIGHT;
            }
            
            int x = Settings.WindowX;
            int y = Settings.WindowY;

            if (x <= 0 || y <= 0)
            {
                x = (sw - w - clientExtraW) / 2;
                y = (sh - h - clientExtraH) / 2;
            }
            if (x + w + clientExtraW > sw)
                x = sw - w - clientExtraW;
            if (y + h + clientExtraH > sh)
                y = sh - h - clientExtraH;

            ClientSize = new System.Drawing.Size(w, h);
            Location = new System.Drawing.Point(x, y);
        }

        public bool IsMinimized { get { return WindowState == FormWindowState.Minimized; } }

        private void Form_Load(object sender, EventArgs e)
        {
            ResizeBegin += (o, ee) => { resizing++; }; 
            ResizeEnd   += (o, ee) => { resizing--; }; 

            keyboard = new KeyboardDX();
            View.OnUserCommand += OnUserCommand;
            screen.Initialize(this);
            HardReset();

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
                case UserCommand.ToggleFullScreen:
                    ToggleFullScreen();
                    break;
                case UserCommand.Window:
                    if (screen.IsFullScreen)
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
        
        private KeyCode repeatKey = KeyCode.None;
        private uint repeatKeyCount = 0;
        
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
                foreach (var ks in keyboard)
                {
                    if (IsActive)
                    {
                        ProcessKey(ks);
                    }
                }
                if (IsActive)
                {
                    if (repeatKey != KeyCode.None)
                    {
                        if (++repeatKeyCount > REPEAT_THRESHOLD)
                            ProcessRepeatKey(repeatKey);
                    }
                }
            }
        }
        private void ProcessRepeatKey(KeyCode Key)
        {
            ProcessKey(new KeyState(Key, keyboard.IsShifted, keyboard.IsControlPressed, keyboard.IsAltPressed, true, true));
        }
        private void ProcessKey(KeyState k)
        {
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
                    case KeyCode.F8:
                    case KeyCode.F9:
                    case KeyCode.F10:
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

            keyboard.Refresh();

            computer.ResetKeyboard();

            if (View.CurrentMode == ViewMode.Normal)
            {
                ProcessKey(new KeyState(KeyCode.LeftShift,    false, false, false, keyboard.LeftShiftPressed));
                ProcessKey(new KeyState(KeyCode.RightShift,   false, false, false, keyboard.RightShiftPressed));
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
                    ClientSize = new System.Drawing.Size((int)(previousClientHeight * (screen.AdvancedView ? ScreenMetrics.WINDOWED_ASPECT_RATIO_ADVANCED : ScreenMetrics.WINDOWED_ASPECT_RATIO_NORMAL)),
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
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Storage.SaveFloppies(computer))
            {
                Settings.WindowX = Location.X;
                Settings.WindowY = Location.Y;
                Settings.WindowWidth = ClientSize.Width;
                Settings.WindowHeight = ClientSize.Height;
                Settings.Save();
                Log.Save(true, out string _);
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

            float aspectRatio = (screen.AdvancedView ? ScreenMetrics.WINDOWED_WIDTH_ADVANCED : ScreenMetrics.WINDOWED_WIDTH_NORMAL) / ScreenMetrics.WINDOWED_HEIGHT;

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

            if (newH > ScreenMetrics.WINDOWED_HEIGHT * 0.88f && newH < ScreenMetrics.WINDOWED_HEIGHT * 1.102f)
                newH = (int)ScreenMetrics.WINDOWED_HEIGHT;

            if (newH > 2 * ScreenMetrics.WINDOWED_HEIGHT * 0.88f && newH < 2 * ScreenMetrics.WINDOWED_HEIGHT * 1.102f)
                newH = 2 * (int)ScreenMetrics.WINDOWED_HEIGHT;

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
                if (!Storage.SaveFloppies(computer) || !Storage.SaveTapeIfRequired(computer))
                    return;

                computer.Dispose();
            }
            computer = new Computer(this, screen, SCREEN_REFRESH_RATE, Settings.DiskEnabled, Settings.NormalSpeed, Settings.SoundOn)
            {
                DriveNoise =   Settings.DriveNoise,
                BreakPoint =   Settings.Breakpoint,
                BreakPointOn = Settings.BreakpointOn
            };

            computer.StartupLoadFloppies();
            screen.Reinitialize(computer);

            Log.Initalize(computer.GetElapsedTStates);

            View.Initialize(computer, (msg) => screen.StatusMessage = msg);

            if (Settings.AutoStartOnReset)
            {
                computer.Start();
                View.CurrentMode = ViewMode.Normal;
            }
        }
    }
}