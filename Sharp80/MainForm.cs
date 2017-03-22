/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sharp80
{
    public partial class MainForm : Form, IAppWindow
    {
        public event MessageEventHandler Sizing;

        private static Thread uiThread;

        private Computer computer;
        private IScreen screen;
        private IKeyboard keyboard;

        private int resizing = 0;

        private Task ScreenTask;
        private Task KeyboardPollTask;
        private System.Windows.Forms.Timer CheckExceptionsTimer;
        private CancellationTokenSource StopToken;

        private bool isDisposing = false;

        private int previousClientHeight;

        private const uint SCREEN_REFRESH_RATE_HZ = 30;
        private const uint KEYBOARD_REFRESH_RATE_HZ = 40;
        private const uint DISPLAY_MESSAGE_CYCLE_DURATION = SCREEN_REFRESH_RATE_HZ;  // 1 second
        
        public static bool IsUiThread => Thread.CurrentThread == uiThread;
        public bool IsMinimized => WindowState == FormWindowState.Minimized;

        private bool IsActive { get; set; }

        public MainForm()
        {
            uiThread = Thread.CurrentThread;
            KeyPreview = true;
            Text = "Sharp 80 - TRS-80 Model III Emulator";

            Dialogs.Initialize(this);
            Dialogs.BeforeShowDialog += BeforeDialog;
            Dialogs.AfterShowDialog += AfterDialog;

            keyboard = new KeyboardDX();

            screen = new ScreenDX(Settings.AdvancedView,
                                  DISPLAY_MESSAGE_CYCLE_DURATION,
                                  Settings.GreenScreen);

            InitializeComponent();
            SetupClientArea();
        }
        
        private void Form_Load(object sender, EventArgs e)
        {
            try
            {
                ResizeBegin += (o, ee) => { screen.Suspend = true;  resizing++; };
                ResizeEnd += (o, ee) => { resizing--; screen.Suspend = false; };

                View.OnUserCommand += OnUserCommand;
                screen.Initialize(this);
                HardReset();
                
                if (Settings.FullScreen)
                    ToggleFullScreen();

                if (Settings.AutoStartOnReset)
                    AutoStart();

                UpdateDialogLevel();

                StopToken = new CancellationTokenSource();

                ScreenTask = screen.Start(SCREEN_REFRESH_RATE_HZ, StopToken.Token);
                KeyboardPollTask = keyboard.Start(KEYBOARD_REFRESH_RATE_HZ, ProcessKey, StopToken.Token);
                CheckExceptionsTimer = new System.Windows.Forms.Timer()
                {
                    Interval = 100
                };
                CheckExceptionsTimer.Tick += (o,ee) => ExceptionHandler.HandleExceptions();
                CheckExceptionsTimer.Start();
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex);
            }
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
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Storage.SaveChangedStorage(computer))
            {
                Settings.WindowX = Location.X;
                Settings.WindowY = Location.Y;
                Settings.WindowWidth = ClientSize.Width;
                Settings.WindowHeight = ClientSize.Height;
                Settings.Save();
                Log.Save(true, out string _);
                Task.Run(Stop);
            }
            else
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == MessageEventArgs.WM_SIZING)
                Sizing?.Invoke(this, new MessageEventArgs(m));

            base.WndProc(ref m);
        }
        
        private void ProcessKey(KeyState k)
        {
            if (IsActive)
            {
                try
                {
                    View.ProcessKey(k);
                }
                catch (Exception ex)
                {
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.Terminate);
                }
            }
        }
        private void SyncKeyboard()
        {
            keyboard.Refresh();
            computer?.ResetKeyboard(keyboard.RightShiftPressed, keyboard.LeftShiftPressed);
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
            SuppressCursor = fs;
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
        
        private void Form_Activated(object sender, EventArgs e)
        {
            SyncKeyboard();
            IsActive = true;
        }
        private void Form_Deactivate(object sender, EventArgs e)
        {
            IsActive = false;
            computer.ResetKeyboard(false, false);
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

            if (newH < ScreenMetrics.WINDOWED_HEIGHT / 2)
                newH = (int)ScreenMetrics.WINDOWED_HEIGHT / 2;
            else if (newH > ScreenMetrics.WINDOWED_HEIGHT * 0.88f && newH < ScreenMetrics.WINDOWED_HEIGHT * 1.102f)
                newH = (int)ScreenMetrics.WINDOWED_HEIGHT;
            else if (newH > 2 * ScreenMetrics.WINDOWED_HEIGHT * 0.88f && newH < 2 * ScreenMetrics.WINDOWED_HEIGHT * 1.102f)
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
            try
            {
                if (computer != null)
                {
                    if (!Storage.SaveChangedStorage(computer) || !Storage.SaveTapeIfRequired(computer))
                        return;

                    computer.Dispose();
                }
                computer = new Computer(this, screen, Settings.DiskEnabled, Settings.NormalSpeed, Settings.SoundOn)
                {
                    DriveNoise = Settings.DriveNoise,
                    BreakPoint = Settings.Breakpoint,
                    BreakPointOn = Settings.BreakpointOn
                };

                computer.StartupInitializeStorage();
                screen.Reinitialize(computer);

                Log.Initalize(computer.GetElapsedTStates);

                View.Initialize(computer, (msg) => screen.StatusMessage = msg);

                if (Settings.AutoStartOnReset)
                {
                    computer.Start();
                    View.CurrentMode = ViewMode.Normal;
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex);
            }
        }
        private void AutoStart()
        {
            var startTimer = new System.Windows.Forms.Timer() { Interval = 500 };
            startTimer.Tick += (s, ee) =>
            {
                startTimer.Stop();
                startTimer.Dispose();
                startTimer = null;
                if (computer.Ready)
                    computer.Start();
                else if (!Disposing)
                    AutoStart();
            };
            startTimer.Start();
        }

        // CURSOR & DIALOG MANAGEMENT

        private static bool suppressCursor = false;
        private static int dialogLevel = 0;
        private static bool cursorHidden = false;

        private bool SuppressCursor
        {
            get { return suppressCursor; }
            set
            {
                suppressCursor = value;
                UpdateDialogLevel();
            }
        }
        private void BeforeDialog()
        {
            // Force show cursor
            dialogLevel++;
            UpdateDialogLevel();
        }
        private void AfterDialog()
        {
            // No force show cursor
            dialogLevel--;
            UpdateDialogLevel();
        }
        private void UpdateDialogLevel()
        {
            if (dialogLevel < 0)
                throw new Exception("Dialog level less than zero.");

            if (suppressCursor && dialogLevel == 0)
            {
                if (!cursorHidden)
                {
                    Cursor.Hide();
                    cursorHidden = true;
                }
            }
            else if (cursorHidden)
            {
                Cursor.Show();
                cursorHidden = false;
            }

            keyboard.Enabled = dialogLevel == 0;
        }

        private async Task Stop()
        {
            try
            {
                CheckExceptionsTimer.Stop();
                StopToken.Cancel();
                await Task.WhenAll(ScreenTask, KeyboardPollTask);
                Dispose();
            }
            catch (AggregateException e)
            {
                foreach (var ee in e.InnerExceptions)
                    if (!(ee is TaskCanceledException))
                        ExceptionHandler.Handle(ee);
            }
            finally
            {
                StopToken.Dispose();
            }
        }
    }
}