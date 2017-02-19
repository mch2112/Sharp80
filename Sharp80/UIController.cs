using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal sealed class UIController : IDisposable
    {
        public Computer Computer { get; set; }
        private IDXClient parent;
        
        private readonly ulong refreshRate;

        public UIController(IDXClient Parent, ulong RefreshRate)
        {
            parent = Parent;
            refreshRate = RefreshRate;
        }
        
        public void Start()
        {
            Computer.CancelStepOverOrOut();
            Computer.Start();
        }
        
#if CASSETTE
        public void LoadCassette(string FilePath)
        {
            computer.LoadCassette(FilePath);
        }
        public bool RewindCassette()
        {
            return computer.RewindCassette();
        }
#endif
        public void HardReset(ScreenDX Screen)
        {
            if (Computer != null)
                Computer.Dispose();
            
            Computer = new Computer(parent, Screen, refreshRate, Settings.Throttle);
            Computer.StartupLoadFloppies();
            Computer.Sound.UseDriveNoise = Settings.DriveNoise;
            Computer.Processor.BreakPoint = Settings.Breakpoint;
            Computer.Processor.BreakPointOn = Settings.BreakpointOn;
        }
        public void ResetKeyboard()
        {
            Computer.ResetKeyboard();
        }
        public bool MakeFloppyFromFile(string FilePath)
        {
            byte[] diskImage = DMK.MakeFloppyFromFile(Storage.LoadBinaryFile(FilePath), System.IO.Path.GetFileName(FilePath)).Serialize(ForceDMK: true);

            if (diskImage.Length > 0)
                return Storage.SaveBinaryFile(System.IO.Path.ChangeExtension(FilePath, "DMK"), diskImage);
            else
                return false;
        }
        
        public bool IsDisposed
        {
            get { return Computer.IsDisposed; }
        }
        public void Dispose()
        {
            if (!Computer.IsDisposed)
            {
                if (Computer.Ready)
                {
                    Computer.HardwareReset();
                    Computer.ShutDown();
                }
                Computer.Dispose();
            }
        }
    }
}
