/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using SharpDX;
using SharpDX.Direct3D10;
using SharpDX.Direct2D1;
using SharpDX.DXGI;

using Device3D = SharpDX.Direct3D10.Device1;
using Device2D = SharpDX.Direct2D1.Device;
using DriverType = SharpDX.Direct3D10.DriverType;

namespace Sharp80
{
    public abstract class Direct3D : IDisposable
    {
        protected Texture2D backBuffer;
        protected Device3D device3D;
        protected Device2D device2D;
        protected DeviceContext context2D;
        protected Bitmap1 bitmap2D;

        protected Size2F Size { get; private set; }
        
        private SwapChain swapChain;
        private SwapChainDescription swapChainDescription;
        private RenderTargetView backBufferView;

        private bool isDisposed;
        private int isResizing;

        protected RenderTarget RenderTarget { get; private set; }
        protected bool IsDrawing { get; private set; }        
        protected IDXClient ParentForm { get; private set; }

        private SharpDX.Direct2D1.Factory d2DFactory = null;

        public Direct3D()
        {
            IsDrawing = false;
            isResizing = 0;
        }
        protected void SetParentForm(IDXClient Form)
        {
            ParentForm = Form;
            ParentForm.BackColor = System.Drawing.Color.Black;
        }
        protected void Initialize(Size2F Size)
        {
            this.Size = Size;
            
            InitializeDX();

            ParentForm.ResizeBegin += (o, args) => { isResizing++; };
            ParentForm.ResizeEnd += (o, args) =>
            {
                isResizing = Math.Max(0, isResizing - 1);
                DoLayout();
            };
            ParentForm.Sizing += (o, args) => { ConstrainAspectRatio(args.Message); };
        }

        public void Stop()
        {
            Dispose();
        }
        protected virtual void Resize(Size2F Size)
        {
            if (!RenderTarget.IsDisposed)
                RenderTarget.Dispose();

            RenderTarget = null;

            WaitForDrawDone();

            // Dispose all previous allocated resources

            System.Diagnostics.Debug.WriteLine("Disposing back buffer...");

            backBuffer.Dispose();
            backBufferView.Dispose();

            backBuffer = null;
            backBufferView = null;

            SwapChainFlags flags;

            flags = SwapChainFlags.AllowModeSwitch;

            this.Size = Size;

            // Resize the backbuffer
            System.Diagnostics.Debug.WriteLine(string.Format("Resizing swap chain buffers. W:{0} H:{1}.", Size.Width, Size.Height));
            try
            {
                swapChain.ResizeBuffers(swapChainDescription.BufferCount,
                                        (int)Size.Width,
                                        (int)Size.Height,
                                        Format.Unknown,
                                        flags);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            CreateBackBuffer();
            CreateRenderTarget(Format.Unknown);
        }
        private void CreateBackBuffer()
        {
            // Get the backbuffer from the swapchain
            backBuffer = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);

            // Renderview on the backbuffer
            backBufferView = new RenderTargetView(device3D, backBuffer);
        }
        public void Dispose()
        {
            if (!isDisposed)
            {
                Dispose(true);
                isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
        public bool IsDisposed { get { return isDisposed; } }

        protected void WaitForDrawDone()
        {
            while (IsDrawing)
                System.Threading.Thread.Sleep(0);
        }
        protected abstract void Draw();

        private bool DrawOK { get { return isResizing == 0 && !IsDrawing && !ParentForm.IsMinimized; } }

        public void Render()
        {
            if (DrawOK)
            {
                try
                {
                    IsDrawing = true;

                    BeginDraw();
                    Draw();
                    EndDraw();
                }
                catch (Exception ex)
                {
                    Log.LogDebug("Exception in D3D Render Loop: " + ex.ToString());
                }
                finally
                {
                    IsDrawing = false;
                }
            }
        }
        protected virtual void InitializeDX()
        {
            swapChainDescription = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription((int)Size.Width,
                                                      (int)Size.Height,
                                                      new Rational(60,1),
                                                      Format.B8G8R8A8_UNorm),
                IsWindowed = true,
                OutputHandle = ParentForm.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput,
                Flags = SwapChainFlags.AllowModeSwitch
            };

            // Create Device and SwapChain
            Device3D.CreateWithSwapChain(DriverType.Hardware,
                                        DeviceCreationFlags.BgraSupport,
                                        swapChainDescription,
                                        SharpDX.Direct3D10.FeatureLevel.Level_10_1,
                                        out device3D,
                                        out swapChain);

            // Ignore all windows events
            swapChain.GetParent<SharpDX.DXGI.Factory>().MakeWindowAssociation(this.ParentForm.Handle,
                                          WindowAssociationFlags.IgnoreAll);

            CreateBackBuffer();
            CreateRenderTarget(Format.Unknown);
        }
        protected virtual void BeginDraw()
        {
            device3D.Rasterizer.SetViewports(new Viewport(0, 0, (int)Size.Width, (int)Size.Height));
            device3D.OutputMerger.SetTargets(backBufferView);
            RenderTarget.BeginDraw();
        }
        protected virtual void EndDraw()
        {
            RenderTarget.EndDraw();
            swapChain.Present(0, PresentFlags.None);
        }
        protected virtual void Dispose(bool disposeManagedResources)
        {
            if (disposeManagedResources)
            {
                if (ParentForm != null)
                    ParentForm.Dispose();
            }

            if (!backBufferView.IsDisposed)
                backBufferView.Dispose();
        }
        
        protected abstract void DoLayout();
        protected abstract void ConstrainAspectRatio(System.Windows.Forms.Message M);

        private void CreateRenderTarget(Format Format)
        {
            d2DFactory = d2DFactory ?? new SharpDX.Direct2D1.Factory();

            using (var surface = backBuffer.QueryInterface<Surface>())
            {
                var rtp = new RenderTargetProperties(new PixelFormat(Format, SharpDX.Direct2D1.AlphaMode.Premultiplied));
                RenderTarget = new RenderTarget(d2DFactory, surface, rtp);
            }
        }

        ~Direct3D()
        {
            if (!isDisposed)
            {
                Dispose(false);
                isDisposed = true;
            }
        }
    }
}