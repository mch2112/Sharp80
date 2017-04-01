using System;
using Sharp80.TRS80;
namespace Sharp80Tests
{
    internal class Settings : ISettings
    {
        public bool AdvancedView { get; set; } = false;
        public bool AltKeyboardLayout { get; set; } = false;
        public bool AutoStartOnReset { get; set; } = false;
        public ushort Breakpoint { get; set; } = 0;
        public bool BreakpointOn { get; set; } = false;
        public string DefaultFloppyDirectory { get; set; } = "";
        public string Disk0Filename
        {
            get => String.Empty;
            set { }
        }
        public string Disk1Filename
        {
            get => String.Empty;
            set { }
        }
        public string Disk2Filename
        {
            get => String.Empty;
            set { }
        }
        public string Disk3Filename
        {
            get => String.Empty; set { }
        }
        public bool DiskEnabled { get; set; }
        public bool DriveNoise { get; set; } = false;
        public bool FullScreen { get; set; } = false;
        public bool GreenScreen { get; set; } = false;
        public string LastAsmFile { get; set; } = "";
        public string LastCmdFile { get; set; } = "";
        public string LastSnapshotFile { get; set; } = "";
        public string LastTapeFile { get; set; } = "";
        public bool NormalSpeed { get; set; } = true;
        public bool SoundOn { get => false; set { } }
        public int WindowHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int WindowWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int WindowX { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int WindowY { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
