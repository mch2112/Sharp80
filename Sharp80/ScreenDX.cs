/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Color = SharpDX.Color;
using DXBitmap = SharpDX.Direct2D1.Bitmap;

namespace Sharp80
{
    
    internal sealed class ScreenDX : Direct3D, IScreen
    {
        private const Format PixelFormat = Format.R8G8B8A8_UNorm;

        public const byte NUM_SCREEN_CHARS_X = 0x40;
        public const byte NUM_SCREEN_CHARS_Y = 0x10;
        public const ushort NUM_SCREEN_CHARS = NUM_SCREEN_CHARS_X * NUM_SCREEN_CHARS_Y;

        private const byte CHAR_PIXELS_X = 0x08;
        private const byte CHAR_PIXELS_Y = 0x18;

        private const float VIRTUAL_SCREEN_WIDTH = NUM_SCREEN_CHARS_X * CHAR_PIXELS_X;
        private const float VIRTUAL_SCREEN_HEIGHT = NUM_SCREEN_CHARS_Y * CHAR_PIXELS_Y;
        private const float VIRTUAL_SCREEN_ASPECT_RATIO = VIRTUAL_SCREEN_WIDTH / VIRTUAL_SCREEN_HEIGHT;

        private const float DISPLAY_SPACING = 10f;
        private const float ADV_INFO_WIDTH = 310f;
        private const float SCREEN_AND_ADV_INFO_ASPECT_RATIO = (VIRTUAL_SCREEN_WIDTH + DISPLAY_SPACING + ADV_INFO_WIDTH) / VIRTUAL_SCREEN_HEIGHT;

        // Windowed values
        public const float WINDOWED_HEIGHT = VIRTUAL_SCREEN_HEIGHT + 24;
        public const float WINDOWED_WIDTH_NORMAL = VIRTUAL_SCREEN_WIDTH + 48;
        public const float WINDOWED_WIDTH_ADVANCED = WINDOWED_WIDTH_NORMAL + DISPLAY_SPACING + ADV_INFO_WIDTH + 24 - 48;

        public const float WINDOWED_ASPECT_RATIO_NORMAL = WINDOWED_WIDTH_NORMAL / WINDOWED_HEIGHT;
        public const float WINDOWED_ASPECT_RATIO_ADVANCED = WINDOWED_WIDTH_ADVANCED / WINDOWED_HEIGHT;

        private readonly uint messageDisplayDuration = 30;

        private TextFormat textFormat, statusTextFormat;

        private ViewMode viewMode;

        public Computer Computer { get; private set; }

        private bool advancedView;

        private bool initialized = false;
        private bool invalid = true;
        private bool invalidateNextDraw = false;
        private bool erase = false;

        private DXBitmap[] charGen, charGenNormal, charGenWide, charGenKanji, charGenKanjiWide;
        private RawRectangleF infoRect, z80Rect, disassemRect, statusMsgRect;
        private RawRectangleF[] cells, cellsNormal, cellsWide;
        System.Drawing.Bitmap[] cgNormal, cgWide, cgKanji, cgKanjiWide;
        private bool loadingCharGen = false;

        private byte[] shadowScreen;

        private uint cyclesForMessageRemaining = 0;

        private SolidColorBrush foregroundBrush,
                                foregroundBrushWhite,
                                foregroundBrushGreen,
                                backgroundBrush,
                                statusBrush,
                                driveOnBrush,
                                driveActiveBrush;

        private Ellipse driveLightEllipse;

        private bool isFullScreen = false;
        private bool isGreenScreen = false;
        private bool isWideCharMode = false;
        private bool isKanjiCharMode = false;

        private string statusMessage = String.Empty;

        public ScreenDX(bool AdvancedView, ViewMode ViewMode, uint MessageDisplayDuration, bool GreenScreen)
        {
            advancedView = AdvancedView;
            viewMode = ViewMode;
            messageDisplayDuration = MessageDisplayDuration;
            isGreenScreen = GreenScreen;

            cellsNormal = new RawRectangleF[NUM_SCREEN_CHARS];
            cellsWide = new RawRectangleF[NUM_SCREEN_CHARS];
            shadowScreen = new byte[NUM_SCREEN_CHARS];

            View.OnUserCommand += View_OnUserCommand;
        }

        private void View_OnUserCommand(UserCommand Command)
        {
            switch (Command)
            {
                case UserCommand.ToggleAdvancedView:
                    AdvancedView = !AdvancedView;
                    Settings.AdvancedView = AdvancedView;
                    break;
                case UserCommand.GreenScreen:
                    Settings.GreenScreen = GreenScreen = !GreenScreen;
                    break;
            }
        }

        public void Initialize(IDXClient Form, Computer Computer)
        {
            SetParentForm(Form); // need to do this before computing targetsize
            this.Computer = Computer;
            Initialize(DesiredLogicalSize);

            InitCharGen();
            LoadCharGen();

            SetVideoMode(false, false);

            initialized = true;

            Invalidate();
        }

        public bool GreenScreen
        {
            get { return isGreenScreen; }
            set
            {
                if (value != isGreenScreen && !loadingCharGen)
                {
                    isGreenScreen = value;
                    StatusMessage = "Changing screen color...";
                    Invalidate();

                    LoadCharGen();
                    foregroundBrush = isGreenScreen ? foregroundBrushGreen : foregroundBrushWhite;

                    StatusMessage = "Screen color changed.";
                    Invalidate();
                }
            }
        }
        public bool IsFullScreen
        {
            get { return isFullScreen; }
            set
            {
                if (isFullScreen != value)
                {
                    isFullScreen = value;
                    Resize(DesiredLogicalSize);
                    if (IsFullScreen)
                        System.Windows.Forms.Cursor.Hide();
                    else
                        System.Windows.Forms.Cursor.Show();
                }
            }
        }
        public bool AdvancedView
        {
            get { return advancedView; }
            set
            {
                if (advancedView != value)
                {
                    advancedView = value;

                    if (!IsFullScreen)
                        ParentForm.ClientSize =
                            new Size((int)(ParentForm.ClientSize.Height * (advancedView ? WINDOWED_ASPECT_RATIO_ADVANCED : WINDOWED_ASPECT_RATIO_NORMAL)),
                                     ParentForm.ClientSize.Height);
                    Resize(DesiredLogicalSize);
                }
            }
        }
        protected override void Resize(Size2F Size)
        {
            WaitForDrawDone();
            base.Resize(Size);

            DoLayout();
        }
        protected override void ConstrainAspectRatio(System.Windows.Forms.Message Msg)
        {
            float ratio;
            if (AdvancedView)
                ratio = WINDOWED_ASPECT_RATIO_ADVANCED;
            else
                ratio = WINDOWED_ASPECT_RATIO_NORMAL;

            float width = ParentForm.ClientSize.Width;
            float height = ParentForm.ClientSize.Height;

            if (Msg.Msg == MessageEventArgs.WM_SIZING)
            {
                var rc = (MessageEventArgs.RECT)Marshal.PtrToStructure(Msg.LParam, typeof(MessageEventArgs.RECT));
                int res = Msg.WParam.ToInt32();
                if (res == MessageEventArgs.WMSZ_LEFT || res == MessageEventArgs.WMSZ_RIGHT)
                {
                    // Left or right resize - adjust height (bottom)
                    rc.Bottom = rc.Top + (int)(width / ratio);
                }
                else if (res == MessageEventArgs.WMSZ_TOP || res == MessageEventArgs.WMSZ_BOTTOM)
                {
                    // Up or down resize - adjust width (right)
                    rc.Right = rc.Left + (int)(height * ratio);
                }
                else if (res == MessageEventArgs.WMSZ_RIGHT + MessageEventArgs.WMSZ_BOTTOM)
                {
                    // Lower-right corner resize -> adjust height (could have been width)
                    rc.Bottom = rc.Top + (int)(width / ratio);
                }
                else if (res == MessageEventArgs.WMSZ_LEFT + MessageEventArgs.WMSZ_TOP)
                {
                    // Upper-left corner -> adjust width (could have been height)
                    rc.Left = rc.Right - (int)(height * ratio);
                }
                Marshal.StructureToPtr(rc, Msg.LParam, true);
            }
        }
        protected override void InitializeDX()
        {
            base.InitializeDX();

            var directWriteFactory = new SharpDX.DirectWrite.Factory();

            foregroundBrushWhite = new SolidColorBrush(RenderTarget, Color.White);
            foregroundBrushGreen = new SolidColorBrush(RenderTarget, new RawColor4(0.3f, 1.0f, 0.3f, 1f));
            backgroundBrush = new SolidColorBrush(RenderTarget, Color4.Black);
            statusBrush = new SolidColorBrush(RenderTarget, Color4.White) { Opacity = 1f };
            driveOnBrush = new SolidColorBrush(RenderTarget, new RawColor4(0.4f, 0.4f, 0.4f, 0.3f));
            driveActiveBrush = new SolidColorBrush(RenderTarget, new RawColor4(1f, 0, 0, 0.3f));

            foregroundBrush = GreenScreen ? foregroundBrushGreen : foregroundBrushWhite;

            textFormat = new TextFormat(directWriteFactory, "Consolas", 12)
            {
                WordWrapping = WordWrapping.NoWrap,
                TextAlignment = TextAlignment.Leading
            };
            statusTextFormat = new TextFormat(directWriteFactory, "Calibri", 18)
            {
                WordWrapping = WordWrapping.NoWrap,
                TextAlignment = TextAlignment.Trailing
            };

            RenderTarget.TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode.Cleartype;

            DoLayout();
        }
    
        public void Reset()
        {
            if (isWideCharMode || isKanjiCharMode)
                SetVideoMode(false, false);
        }
        public void SetVideoMode(bool IsWide, bool IsKanji)
        {
            // In basic, "PRINT CHR$(23)" (or Shift-RightArrow) will set wide character mode
            // The CLEAR key will revert to normal width
            // And "PRINT CHR$(22)" will toggle the normal and Kanji sets

            isWideCharMode = IsWide;
            isKanjiCharMode = IsKanji;

            if (IsWide && IsKanji)
            {
                charGen = charGenKanjiWide;
                cells = cellsWide;
            }
            else if (IsWide && !IsKanji)
            {
                charGen = charGenWide;
                cells = cellsWide;
            }
            else if (!IsWide && IsKanji)
            {
                charGen = charGenKanji;
                cells = cellsNormal;
            }
            else
            {
                charGen = charGenNormal;
                cells = cellsNormal;
            }
            Invalidate();
        }
        public string StatusMessage
        {
            get { return statusMessage; }
            set
            {
                statusMessage = value;
                if (value.Length == 0)
                {
                    statusBrush.Opacity = 0;
                    cyclesForMessageRemaining = 0;
                }
                else
                {
                    cyclesForMessageRemaining = messageDisplayDuration;
                    statusBrush.Opacity = 1f;
                }
                Invalidate();
            }
        }
        public void Invalidate()
        {
            invalid = true;
        }

        protected override void Draw()
        {
            if (initialized)
            {
                invalid |= View.Invalid;

                if (invalidateNextDraw)
                {
                    invalidateNextDraw = false;
                    invalid = true;
                }

                if (erase)
                {
                    invalid = true;
                    RenderTarget.Clear(Color.Black);
                }

                var dbs = Computer.DriveBusyStatus;
                if (dbs.HasValue)
                    RenderTarget.FillEllipse(driveLightEllipse, dbs.Value ? driveActiveBrush : driveOnBrush);
                else
                    RenderTarget.FillEllipse(driveLightEllipse, backgroundBrush);

                if (View.CurrentMode == ViewMode.Normal || invalid)
                    DrawView(View.GetViewData());
                
                //renderTarget.DrawRectangle(new RawRectangleF(cells[0].Left, cells[0].Top, cells[0x3ff].Right, cells[0x3ff].Bottom), foregroundBrush);
                //renderTarget.FillRectangle(cells[0], foregroundBrush);
                //renderTarget.FillRectangle(cells[0x3ff], foregroundBrush);

                if (advancedView)
                {
                    //if (!invalid)
                        ClearAdvancedInfoRegions();

                    RenderTarget.DrawText(Computer.GetInternalsReport(), textFormat, z80Rect, foregroundBrush);
                    RenderTarget.DrawText(Computer.GetDisassembly(), textFormat, disassemRect, foregroundBrush);
                    RenderTarget.DrawText(
                        Computer.GetClockReport(true) + Environment.NewLine + Computer.GetDriveStatusReport(),
                        textFormat, infoRect, foregroundBrush);
                }

                DrawStatusMessage();

                invalid = false;
                View.Validate();
            }
        }

        private void DrawNormal()
        {
            var mem = Computer.Memory;

            if (mem.ScreenWritten || invalid)
            {
                mem.ScreenWritten = false;
                int k = 0;
                ushort memPtr = Memory.VIDEO_MEMORY_BLOCK;
                for (int i = 0; i < NUM_SCREEN_CHARS; ++i, ++k, ++memPtr)
                {
                    PaintCell(k, mem[memPtr], cells, charGen);
                    if (isWideCharMode)
                        { i++; k++; memPtr++; }
                }
            }
        }
        private void DrawView(byte[] View)
        {
            if (View == null)
            {
                DrawNormal();
            }
            else
            {
                for (int i = 0; i < NUM_SCREEN_CHARS; i++)
                    PaintCell(i, View[i], cellsNormal, charGenNormal);
            }
        }
        private void DrawStatusMessage()
        {
            if (cyclesForMessageRemaining > 0)
            {
                cyclesForMessageRemaining--;

                RenderTarget.FillRectangle(statusMsgRect, backgroundBrush);
                RenderTarget.DrawText(StatusMessage, statusTextFormat, statusMsgRect, statusBrush);

                statusBrush.Opacity *= 0.95f;

                if (cyclesForMessageRemaining == 0)
                    invalidateNextDraw = true;
            }
        }

        private void ClearAdvancedInfoRegions()
        {
            RenderTarget.FillRectangle(infoRect, backgroundBrush);
            RenderTarget.FillRectangle(z80Rect, backgroundBrush);
            RenderTarget.FillRectangle(disassemRect, backgroundBrush);
        }
        private void PaintCell(int cell, byte c, RawRectangleF[] Cells, DXBitmap[] Chars)
        {
            if (shadowScreen[cell] != c || invalid)
            {
                RenderTarget.DrawBitmap(Chars[c],
                    Cells[cell],
                    1.0f,
                    BitmapInterpolationMode.Linear);

                shadowScreen[cell] = c;
            }
        }
        private void InitCharGen()
        {
            System.Drawing.Bitmap characters = (System.Drawing.Bitmap)System.Drawing.Image.FromStream(new System.IO.MemoryStream(Resources.CharGen));

            cgNormal = new System.Drawing.Bitmap[0x100];
            cgWide = new System.Drawing.Bitmap[0x100];
            cgKanji = new System.Drawing.Bitmap[0x100];
            cgKanjiWide = new System.Drawing.Bitmap[0x100];

            for (int y = 0; y < 0x08; y++)
            {
                for (int x = 0; x < 0x20; x++)
                {
                    int index = x + 0x20 * y;
                    cgNormal[index] = characters.Clone(new System.Drawing.Rectangle(x * CHAR_PIXELS_X,
                                                                              y * CHAR_PIXELS_Y,
                                                                              CHAR_PIXELS_X,
                                                                              CHAR_PIXELS_Y),
                                                                              System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                    cgWide[index] = characters.Clone(new System.Drawing.Rectangle(x * CHAR_PIXELS_X * 2,
                                                                              y * CHAR_PIXELS_Y + 240,
                                                                              CHAR_PIXELS_X * 2,
                                                                              CHAR_PIXELS_Y),
                                                                              System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                    if (y < 6)
                    {
                        cgKanji[index] = cgNormal[index];
                        cgKanjiWide[index] = cgWide[index];
                    }
                    else
                    {
                        cgKanji[index] = characters.Clone(new System.Drawing.Rectangle(x * CHAR_PIXELS_X,
                                                                                 (y + 2) * CHAR_PIXELS_Y,
                                                                                 CHAR_PIXELS_X,
                                                                                 CHAR_PIXELS_Y),
                                                                                 System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                        cgKanjiWide[index] = characters.Clone(new System.Drawing.Rectangle(x * CHAR_PIXELS_X * 2,
                                                                                 (y + 2) * CHAR_PIXELS_Y + 240,
                                                                                 CHAR_PIXELS_X * 2,
                                                                                 CHAR_PIXELS_Y),
                                                                                 System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                    }
                }
            }
        }
        private void LoadCharGen()
        {
            loadingCharGen = true;

            charGenNormal = charGenNormal ?? new DXBitmap[0x100];
            charGenWide = charGenWide ?? new DXBitmap[0x100];
            charGenKanji = charGenKanji ?? new DXBitmap[0x100];
            charGenKanjiWide = charGenKanjiWide ?? new DXBitmap[0x100];

            uint filterABGR = GreenScreen ? 0xFF40FF40 : 0xFFFFFFFF;

            for (int i = 0; i < 0x100; i++)
            {
                charGenNormal[i] = ConvertBitmap(RenderTarget, cgNormal[i], filterABGR);
                charGenWide[i] = ConvertBitmap(RenderTarget, cgWide[i], filterABGR);
                charGenKanji[i] = ConvertBitmap(RenderTarget, cgKanji[i], filterABGR);
                charGenKanjiWide[i] = ConvertBitmap(RenderTarget, cgKanjiWide[i], filterABGR);
            }
            loadingCharGen = false;
        }
        private static DXBitmap ConvertBitmap(RenderTarget renderTarget, System.Drawing.Bitmap bitmap, uint FilterABGR = 0xFFFFFFFF)
        {
            var sourceArea = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapProperties = new BitmapProperties(new PixelFormat(ScreenDX.PixelFormat,
                                                                        SharpDX.Direct2D1.AlphaMode.Premultiplied));
            var size = new Size2(bitmap.Width, bitmap.Height);

            // Transform pixels from BGRA to ABGR
            int stride = bitmap.Width * sizeof(int);
            using (var tempStream = new DataStream(bitmap.Height * stride, true, true))
            {
                // Lock source bitmap
                var bitmapData = bitmap.LockBits(sourceArea,
                                                 System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                                 System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                // Convert all pixels 
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int offset = bitmapData.Stride * y;
                    IntPtr scan0 = bitmapData.Scan0;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        // Not optimized 
                        byte B = Marshal.ReadByte(scan0, offset++);
                        byte G = Marshal.ReadByte(scan0, offset++);
                        byte R = Marshal.ReadByte(scan0, offset++);
                        byte A = Marshal.ReadByte(scan0, offset++);

                        uint abgr = (uint)(R | (G << 8) | (B << 16) | (A << 24));
                        abgr &= FilterABGR;
                        tempStream.Write(abgr);
                    }
                }
                bitmap.UnlockBits(bitmapData);
                tempStream.Position = 0;

                return new DXBitmap(renderTarget, size, tempStream, stride, bitmapProperties);
            }
        }
        protected override void DoLayout()
        {
            float xBorder;
            float yBorder;

            System.Diagnostics.Debug.WriteLine(string.Format("Logical screen {0}x{1}", Size.Width, Size.Height));

            if (IsFullScreen)
            {
                float targetAspect = AdvancedView ? SCREEN_AND_ADV_INFO_ASPECT_RATIO : VIRTUAL_SCREEN_ASPECT_RATIO;
                float logicalAspect = this.Size.Width / this.Size.Height;

                System.Diagnostics.Debug.WriteLine(string.Format("Target Aspect: {0}, Logical Aspect: {1}", targetAspect, logicalAspect));

                if (logicalAspect < targetAspect) // extra vertical space
                {
                    xBorder = 0;
                    var missingXPixels = this.Size.Height * (targetAspect - logicalAspect);
                    var extraYPixels = missingXPixels / targetAspect;
                    yBorder = extraYPixels / 2;
                }
                else // extra horizontal space
                {
                    xBorder = this.Size.Height * (logicalAspect - targetAspect) / 2;
                    yBorder = 0;
                }
            }
            else
            {
                xBorder = advancedView ? 12f : 24f;
                yBorder = 12f;
            }

            System.Diagnostics.Debug.WriteLine(string.Format("Layout Border: {0}x {1}y", xBorder, yBorder));

            float xOrigin = xBorder;
            float yOrigin = yBorder;

            for (int j = 0; j < NUM_SCREEN_CHARS_Y; j++)
            {
                for (int i = 0; i < NUM_SCREEN_CHARS_X; i++)
                {
                    float x = i * CHAR_PIXELS_X + xOrigin;
                    float y = j * CHAR_PIXELS_Y + yOrigin;

                    // Cast floats to ints to prevent bleeding at edges of cells when scaling
                    cellsNormal[i + j * NUM_SCREEN_CHARS_X] = new RawRectangleF((int)x, (int)y, (int)(x + CHAR_PIXELS_X), (int)(y + CHAR_PIXELS_Y));
                    cellsWide[i + j * NUM_SCREEN_CHARS_X] = new RawRectangleF((int)x, (int)y, (int)(x + CHAR_PIXELS_X + CHAR_PIXELS_X), (int)(y + CHAR_PIXELS_Y));
                }
            }

            cells = cells ?? cellsNormal;

            driveLightEllipse = new Ellipse(new RawVector2(10, 10), 5, 5);

            const float SPACING = 10f;
            xOrigin += NUM_SCREEN_CHARS_X * CHAR_PIXELS_X + SPACING;

            const float Z80WIDTH = 70f;

            const float INFO_RECT_HEIGHT = 40;

            z80Rect = new RawRectangleF(xOrigin,
                                        yOrigin + SPACING,
                                        xOrigin + Z80WIDTH,
                                        yOrigin + VIRTUAL_SCREEN_HEIGHT - INFO_RECT_HEIGHT - SPACING);

            disassemRect = new RawRectangleF(z80Rect.Right,
                                              z80Rect.Top,
                                              WINDOWED_WIDTH_ADVANCED,
                                              z80Rect.Bottom);

            infoRect = new RawRectangleF(z80Rect.Left,
                                         yOrigin + VIRTUAL_SCREEN_HEIGHT - INFO_RECT_HEIGHT,
                                         z80Rect.Left + ADV_INFO_WIDTH,
                                         yOrigin + VIRTUAL_SCREEN_HEIGHT);

            // Bottom right corner
            statusMsgRect = new RawRectangleF(Size.Width - 175,
                                              Size.Height - 30,
                                              Size.Width - SPACING,
                                              Size.Height);

            erase = true;
        }
        
        private Size2F DesiredLogicalSize
        {
            get
            {
                Size2F ts;

                float physX;
                float physY;

                if (IsFullScreen)
                {
                    var scn = System.Windows.Forms.Screen.FromHandle(ParentForm.Handle);
                    physX = scn.WorkingArea.Width;
                    physY = scn.WorkingArea.Height;

                    // choose a logical size so that the aspect ratio matches the physical aspect ratio
                    float physicalAspect = physX / physY;
                    float w = VIRTUAL_SCREEN_WIDTH + (advancedView ? ADV_INFO_WIDTH + DISPLAY_SPACING : 0);
                    float h = VIRTUAL_SCREEN_HEIGHT;
                    float targetAspectRatio = w/h;

                    if (physicalAspect > targetAspectRatio) // extra horizontal space
                        w += h * (physicalAspect - targetAspectRatio);
                    else // extra vertical space
                        h = h * targetAspectRatio / physicalAspect;
                    ts = new Size2F(w, h);
                }
                else
                {
                    physX = ParentForm.ClientSize.Width;
                    physY = ParentForm.ClientSize.Height;

                    ts = AdvancedView ? new Size2F(WINDOWED_WIDTH_ADVANCED, WINDOWED_HEIGHT)
                                           : new Size2F(WINDOWED_WIDTH_NORMAL, WINDOWED_HEIGHT);
                }
                System.Diagnostics.Debug.WriteLine(string.Format("Target Size for Physical {0}x{1}, Advanced={2} FullScreen={3}: {4}x{5}",physX, physY, advancedView ? "true" : "false", IsFullScreen ? "true" : "false", ts.Width, ts.Height));
                return ts;
            }
        }
        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(isWideCharMode);
            Writer.Write(isKanjiCharMode);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            SetVideoMode(Reader.ReadBoolean(), Reader.ReadBoolean());
        }
    }
}
