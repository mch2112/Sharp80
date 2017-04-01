/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Sharp80.DirectX;
using Sharp80.TRS80;
using Sharp80.Views;

namespace Sharp80
{
    public partial class MainForm : Form, IAppWindow
    {
        private static Thread uiThread;
        private static MainForm instance;

        private ProductInfo ProductInfo { get; set; }
        private Settings Settings { get; set; }
        private IDialogs Dialogs { get; set; }

        private Computer computer;
        private IScreen screen;
        private IKeyboard keyboard;

        private Task ScreenTask;
        private Task KeyboardPollTask;
        private CancellationTokenSource StopToken;

        private bool isDisposing = false;

        private int previousClientHeight;

        private const uint SCREEN_REFRESH_RATE_HZ = 30;
        private const uint KEYBOARD_REFRESH_RATE_HZ = 40;
        private const uint DISPLAY_MESSAGE_CYCLE_DURATION = SCREEN_REFRESH_RATE_HZ;  // 1 second
        
        public static bool IsUiThread => Thread.CurrentThread == uiThread;
        public static MainForm Instance => instance; 
        public bool IsMinimized => WindowState == FormWindowState.Minimized;

        private bool IsActive { get; set; }

        public MainForm()
        {
            instance = this;
            Settings = new Settings();
            ProductInfo = new ProductInfo();
            Dialogs = new WinDialogs(this, ProductInfo, BeforeDialog, AfterDialog);

            uiThread = Thread.CurrentThread;
            KeyPreview = true;
            Text = ProductInfo.ProductName + " - TRS-80 Model III Emulator";
            BackColor = System.Drawing.Color.Black;
            keyboard = new KeyboardDX();

            screen = new ScreenDX(this,
                                  Dialogs,
                                  Settings,
                                  DISPLAY_MESSAGE_CYCLE_DURATION);

            InitializeComponent();
            SetupClientArea();
        }
        
        private void Form_Load(object sender, EventArgs e)
        {
            try
            {
                Activated += (s, ee) => { SyncKeyboard(); IsActive = true; };
                Deactivate += (s, ee) => { IsActive = false; computer.ResetKeyboard(false, false); };

                Views.View.OnUserCommand += ProcessUserCommand;

                HardReset();

                if (Settings.FullScreen)
                    ToggleFullScreen();

                if (Settings.AutoStartOnReset)
                    AutoStart();

                UpdateDialogLevel();

                StopToken = new CancellationTokenSource();

                ScreenTask = screen.Start(SCREEN_REFRESH_RATE_HZ, StopToken.Token);
                KeyboardPollTask = keyboard.Start(KEYBOARD_REFRESH_RATE_HZ, ProcessKey, StopToken.Token);
            }
            catch (Exception Ex)
            {
                Dialogs.ExceptionAlert(Ex);
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
        
        private void ProcessUserCommand(UserCommand Command)
        {
            switch (Command)
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
            if (computer.SaveChangedStorage())
            {
                Settings.WindowX = Location.X;
                Settings.WindowY = Location.Y;
                Settings.WindowWidth = ClientSize.Width;
                Settings.WindowHeight = ClientSize.Height;
                Settings.Save();
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
                ConstrainAspectRatio(m);
            base.WndProc(ref m);
        }
        private void ProcessKey(KeyState Key)
        {
            if (IsActive)
            {
                try
                {
                    if (Key.Key == KeyCode.Capital && Key.Released)
                        TurnCapsLockOff();
                    Views.View.ProcessKey(Key);
                }
                catch (Exception Ex)
                {
                    Dialogs.ExceptionAlert(Ex);
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
                    var r = ConstrainToScreen(Location.X,
                                          Location.Y,
                                          previousClientHeight);
                    
                    ClientSize = r.Size;
                    Location = r.Location;
                }
            }
            SuppressCursor = fs;
        }
        private void SetWindowStyle()
        {
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
                if (!screen.IsFullScreen)
                    ToggleFullScreen(true);
                return;
            }

            if (screen.IsFullScreen)
                ToggleFullScreen(false);

            int xOffset = (newW - curW) / 2;
            int yOffset = (newH - curH) / 2;

            int newX = curX - xOffset;
            int newY = curY - yOffset;

            SetWindowStyle();

            var r = ConstrainToScreen(newX, newY, newH);
            ClientSize = r.Size;
            Location = r.Location;
            previousClientHeight = r.Height;
        }
        private void HardReset()
        {
            try
            {
                if (computer != null)
                {
                    if (!computer.SaveChangedStorage())
                        return;
                    computer.Dispose();
                }
                var sound = new SoundX(16000);
                if (sound.Stopped)
                    Dialogs.AlertUser("Sound failed to start. Continuing without sound.");
                
                computer = new Computer(screen, sound, new Timer(), Settings, Dialogs);
                
                screen.Initialize(computer);

                Views.View.Initialize(ProductInfo, Dialogs, Settings, computer, (msg) => screen.StatusMessage = msg);

                if (Settings.AutoStartOnReset)
                {
                    computer.Start();
                    Views.View.CurrentMode = ViewMode.Normal;
                }
            }
            catch (Exception Ex)
            {
                Dialogs.ExceptionAlert(Ex);
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
            System.Diagnostics.Debug.Assert(dialogLevel >= 0);
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
                StopToken.Cancel();
                await Task.WhenAll(ScreenTask, KeyboardPollTask);
                Dispose();
            }
            catch (AggregateException e)
            {
                foreach (var ee in e.InnerExceptions)
                    if (!(ee is TaskCanceledException))
                    {
                        // should do something
                    }
            }
            finally
            {
                StopToken.Dispose();
            }
        }

        private void TurnCapsLockOff()
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x01;
            const int KEYEVENTF_KEYUP = 0x02;

            // Annoying that it turns on when doing virtual shift-zero.
            if (IsKeyLocked(Keys.CapsLock))
            {
                NativeMethods.keybd_event(0x14, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                NativeMethods.keybd_event(0x14, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP,
                    (UIntPtr)0);
            }
        }
        private System.Drawing.Rectangle ConstrainToScreen(int X, int Y, int Height)
        {
            var scn = Screen.FromHandle(Handle);

            // screen area adjusted for difference between size and client size
            int screenW = scn.WorkingArea.Width - Size.Width + ClientSize.Width;
            int screenH = scn.WorkingArea.Height - Size.Height + ClientSize.Height;

            float h = Height;
            float aspectRatio = screen.AdvancedView ? ScreenMetrics.WINDOWED_ASPECT_RATIO_ADVANCED : ScreenMetrics.WINDOWED_ASPECT_RATIO_NORMAL;
            float w = h * aspectRatio;

            if (w > screenW)
            {
                w = screenW;
                h = w / aspectRatio;
            }
            if (h > screenH)
            {
                h = screenH;
                w = h * aspectRatio;
            }
            X = Math.Min(Math.Max(0, X), screenW - (int)w);
            Y = Math.Min(Math.Max(0, X), screenH - (int)h);
            return new System.Drawing.Rectangle(X, Y, (int)w, (int)h);
        }
        private void ConstrainAspectRatio(Message Msg)
        {
            float ratio = screen.AdvancedView ? ScreenMetrics.WINDOWED_ASPECT_RATIO_ADVANCED : ScreenMetrics.WINDOWED_ASPECT_RATIO_NORMAL;

            if (Msg.Msg == MessageEventArgs.WM_SIZING)
            {
                var rc = (MessageEventArgs.RECT)Marshal.PtrToStructure(Msg.LParam, typeof(MessageEventArgs.RECT));
                int res = Msg.WParam.ToInt32();
                if (res == MessageEventArgs.WMSZ_LEFT || res == MessageEventArgs.WMSZ_RIGHT)
                {
                    // Left or right resize - adjust height (bottom)
                    rc.Bottom = rc.Top + (int)(ClientSize.Width / ratio);
                }
                else if (res == MessageEventArgs.WMSZ_TOP || res == MessageEventArgs.WMSZ_BOTTOM)
                {
                    // Up or down resize - adjust width (right)
                    rc.Right = rc.Left + (int)(ClientSize.Height * ratio);
                }
                else if (res == MessageEventArgs.WMSZ_RIGHT + MessageEventArgs.WMSZ_BOTTOM)
                {
                    // Lower-right corner resize -> adjust height
                    rc.Bottom = rc.Top + (int)(ClientSize.Width / ratio);
                }
                else if (res == MessageEventArgs.WMSZ_LEFT + MessageEventArgs.WMSZ_TOP)
                {
                    // Upper-left corner -> adjust width (left)
                    rc.Left = rc.Right - (int)(ClientSize.Height * ratio);
                }
                Marshal.StructureToPtr(rc, Msg.LParam, true);
            }
        }
    }
}