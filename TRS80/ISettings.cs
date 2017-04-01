namespace Sharp80.TRS80
{
    public interface ISettings
    {
        bool AdvancedView { get; set; }
        bool AltKeyboardLayout { get; set; }
        bool AutoStartOnReset { get; set; }
        ushort Breakpoint { get; set; }
        bool BreakpointOn { get; set; }
        string DefaultFloppyDirectory { get; set; }
        string Disk0Filename { get; set; }
        string Disk1Filename { get; set; }
        string Disk2Filename { get; set; }
        string Disk3Filename { get; set; }
        bool DiskEnabled { get; set; }
        bool DriveNoise { get; set; }
        bool FullScreen { get; set; }
        bool GreenScreen { get; set; }
        string LastAsmFile { get; set; }
        string LastCmdFile { get; set; }
        string LastSnapshotFile { get; set; }
        string LastTapeFile { get; set; }
        bool NormalSpeed { get; set; }
        bool SoundOn { get; set; }
        int WindowHeight { get; set; }
        int WindowWidth { get; set; }
        int WindowX { get; set; }
        int WindowY { get; set; }
    }
}