/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal class Settings : TRS80.ISettings
    {
        public bool AdvancedView { get; set; } = Properties.Settings.Default.advanced_view;
        public bool AltKeyboardLayout { get; set; } = Properties.Settings.Default.alt_keyboard_layout;
        public bool AutoStartOnReset { get; set; } = Properties.Settings.Default.auto_start_on_reset;
        public ushort Breakpoint { get; set; } = Properties.Settings.Default.breakpoint;
        public bool BreakpointOn { get; set; } = Properties.Settings.Default.breakpoint_on;
        public TRS80.ClockSpeed ClockSpeed { get; set; } = TRS80.ClockSpeed.Normal;
        public string DefaultFloppyDirectory { get; set; } = Properties.Settings.Default.default_floppy_directory;
        public string Disk0Filename { get; set; } = Properties.Settings.Default.disk0;
        public string Disk1Filename { get; set; } = Properties.Settings.Default.disk1;
        public string Disk2Filename { get; set; } = Properties.Settings.Default.disk2;
        public string Disk3Filename { get; set; } = Properties.Settings.Default.disk3;
        public bool DiskEnabled { get; set; } = Properties.Settings.Default.disk_enabled;
        public bool DriveNoise { get; set; } = Properties.Settings.Default.drive_noise;
        public bool GreenScreen { get; set; } = Properties.Settings.Default.green_screen;
        public string LastAsmFile { get; set; } = Properties.Settings.Default.last_asm_file;
        public string LastCmdFile { get; set; } = Properties.Settings.Default.last_cmd_file;
        public string LastSnapshotFile { get; set; } = Properties.Settings.Default.last_snapshot_file;
        public string LastTapeFile { get; set; } = Properties.Settings.Default.last_tape_file;
        public bool SoundOn { get; set; } = Properties.Settings.Default.sound;
        public bool FullScreen { get; set; } = Properties.Settings.Default.full_screen;
        public int WindowX { get; set; } = Properties.Settings.Default.window_x;
        public int WindowY { get; set; } = Properties.Settings.Default.window_y;
        public int WindowWidth { get; set; } = Properties.Settings.Default.window_width;
        public int WindowHeight { get; set; } = Properties.Settings.Default.window_height;
        public void Save()
        {
            var psd = Properties.Settings.Default;
            psd.advanced_view = AdvancedView;
            psd.auto_start_on_reset = AutoStartOnReset;
            psd.breakpoint = Breakpoint;
            psd.breakpoint_on = BreakpointOn;
            psd.default_floppy_directory = DefaultFloppyDirectory;
            psd.disk0 = Disk0Filename;
            psd.disk1 = Disk1Filename;
            psd.disk2 = Disk2Filename;
            psd.disk3 = Disk3Filename;
            psd.disk_enabled = DiskEnabled;
            psd.drive_noise = DriveNoise;
            psd.green_screen = GreenScreen;
            psd.last_asm_file = LastAsmFile;
            psd.last_cmd_file = LastCmdFile;
            psd.last_snapshot_file = LastSnapshotFile;
            psd.last_tape_file = LastTapeFile;
            psd.sound = SoundOn;
            psd.clock_speed = (int)ClockSpeed;
            psd.full_screen = FullScreen;
            psd.window_x = WindowX;
            psd.window_y = WindowY;
            psd.window_width = WindowWidth;
            psd.window_height = WindowHeight;
            Properties.Settings.Default.Save();
        }
    }
}
