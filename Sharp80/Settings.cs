/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal class Settings : TRS80.ISettings
    {
        private bool? advancedView;
        private bool? altKeyboardLayout;
        private bool? autoStartOnReset;
        private ushort? breakpoint;
        private bool? breakpointOn;
        private string defaultFloppyDirectory;
        private string disk0Filename;
        private string disk1Filename;
        private string disk2Filename;
        private string disk3Filename;
        private bool? diskEnabled;
        private bool? driveNoise;
        private bool? greenScreen;
        private string lastAsmFile;
        private string lastCmdFile;
        private string lastTapeFile;
        private string lastSnapshotFile;
        private bool? soundOn;
        private bool? normalSpeed;

        private bool? fullScreen;
        private int? windowX;
        private int? windowY;
        private int? windowWidth;
        private int? windowHeight;
        
        public Settings()
        {
#if DEBUG
         //   Properties.Settings.Default.Reset();
#endif
        }

        public bool AdvancedView
        {
            get => advancedView ?? (advancedView = Properties.Settings.Default.advanced_view).Value;
            set => advancedView = value;
        }
        public bool AltKeyboardLayout
        {
            get => altKeyboardLayout ?? (altKeyboardLayout = Properties.Settings.Default.alt_keyboard_layout).Value;
            set => altKeyboardLayout = value;
        }
        public bool AutoStartOnReset
        {
            get => autoStartOnReset ?? (autoStartOnReset = Properties.Settings.Default.auto_start_on_reset).Value;
            set => autoStartOnReset = value;
        }
        public ushort Breakpoint
        {
            get => breakpoint ?? (breakpoint = Properties.Settings.Default.breakpoint).Value;
            set => breakpoint = value;
        }
        public bool BreakpointOn
        {
            get => breakpointOn ?? (breakpointOn = Properties.Settings.Default.breakpoint_on).Value;
            set => breakpointOn = value;
        }
        public string DefaultFloppyDirectory
        {
            get => defaultFloppyDirectory = defaultFloppyDirectory ?? Properties.Settings.Default.default_floppy_directory;
            set => defaultFloppyDirectory = value;
        }
        public string Disk0Filename
        {
            get => disk0Filename = disk0Filename ?? Properties.Settings.Default.disk0;
            set => disk0Filename = value;
        }
        public string Disk1Filename
        {
            get => disk1Filename = disk1Filename ?? Properties.Settings.Default.disk1;
            set => disk1Filename = value;
        }
        public string Disk2Filename
        {
            get => disk2Filename = disk2Filename ?? Properties.Settings.Default.disk2;
            set => disk2Filename = value;
        }
        public string Disk3Filename
        {
            get => disk3Filename = disk3Filename ?? Properties.Settings.Default.disk3;
            set => disk3Filename = value;
        }
        public bool DiskEnabled
        {
            get => diskEnabled ?? (diskEnabled = Properties.Settings.Default.disk_enabled).Value;
            set => diskEnabled = value;
        }
        public bool DriveNoise
        {
            get => driveNoise ?? (driveNoise = Properties.Settings.Default.drive_noise).Value;
            set => driveNoise = value;
        }
        public bool GreenScreen
        {
            get => greenScreen ?? (greenScreen = Properties.Settings.Default.green_screen).Value;
            set => greenScreen = value;
        }
        public string LastAsmFile
        {
            get => lastAsmFile = lastAsmFile ?? Properties.Settings.Default.last_asm_file;
            set => lastAsmFile = value;
        }
        public string LastCmdFile
        {
            get => lastCmdFile = lastCmdFile ?? Properties.Settings.Default.last_cmd_file;
            set => lastCmdFile = value;
        }
        public string LastSnapshotFile
        {
            get => lastSnapshotFile ?? (lastSnapshotFile = Properties.Settings.Default.last_snapshot_file);
            set => lastSnapshotFile = value;
        }
        public string LastTapeFile
        {
            get => lastTapeFile = lastTapeFile ?? Properties.Settings.Default.last_tape_file;
            set => lastTapeFile = value;
        }
        public bool SoundOn
        {
            get => soundOn ?? (soundOn = Properties.Settings.Default.sound).Value;
            set => soundOn = value;
        }
        public bool NormalSpeed
        {
            get => normalSpeed ?? (normalSpeed = Properties.Settings.Default.normal_speed).Value;
            set => normalSpeed = value;
        }
        public bool FullScreen
        {
            get => fullScreen ?? (fullScreen = Properties.Settings.Default.full_screen).Value;
            set => fullScreen = value;
        }
        public int WindowX
        {
            get => windowX ?? (windowX = Properties.Settings.Default.window_x).Value;
            set => windowX = value;
        }
        public int WindowY
        {
            get => windowY ?? (windowY = Properties.Settings.Default.window_y).Value;
            set => windowY = value;
        }
        public int WindowWidth
        {
            get => windowWidth ?? (windowWidth = Properties.Settings.Default.window_width).Value;
            set => windowWidth = value;
        }
        public int WindowHeight
        {
            get => windowHeight ?? (windowHeight = Properties.Settings.Default.window_height).Value;
            set => windowHeight = value;
        }
        public void Save()
        {
            var psd = Properties.Settings.Default;

            if (advancedView.HasValue)
                psd.advanced_view = advancedView.Value;
            if (autoStartOnReset.HasValue)
                psd.auto_start_on_reset = autoStartOnReset.Value;
            if (breakpoint.HasValue)
                psd.breakpoint = breakpoint.Value;
            if (breakpointOn.HasValue)
                psd.breakpoint_on = breakpointOn.Value;
            if (!String.IsNullOrWhiteSpace(defaultFloppyDirectory))
                psd.default_floppy_directory = defaultFloppyDirectory;
            if (disk0Filename != null)
                psd.disk0 = disk0Filename;
            if (disk1Filename != null)
                psd.disk1 = disk1Filename;
            if (disk2Filename != null)
                psd.disk2 = disk2Filename;
            if (disk3Filename != null)
                psd.disk3 = disk3Filename;
            if (diskEnabled.HasValue)
                psd.disk_enabled = diskEnabled.Value;
            if (driveNoise.HasValue)
                psd.drive_noise = driveNoise.Value;
            if (greenScreen.HasValue)
                psd.green_screen = greenScreen.Value;
            if (!String.IsNullOrWhiteSpace(lastAsmFile))
                psd.last_asm_file = lastAsmFile;
            if (!String.IsNullOrWhiteSpace(lastCmdFile))
                psd.last_cmd_file = lastCmdFile;
            if (!String.IsNullOrWhiteSpace(lastSnapshotFile))
                psd.last_snapshot_file = lastSnapshotFile;
            if (!String.IsNullOrWhiteSpace(lastTapeFile))
                psd.last_tape_file = lastTapeFile;
            if (soundOn.HasValue)
                psd.sound = soundOn.Value;
            if (normalSpeed.HasValue)
                psd.normal_speed = normalSpeed.Value;
            if (fullScreen.HasValue)
                psd.full_screen = fullScreen.Value;
            if (windowX.HasValue)
                psd.window_x = windowX.Value;
            if (windowY.HasValue)
                psd.window_y = windowY.Value;
            if (windowWidth.HasValue)
                psd.window_width = windowWidth.Value;
            if (windowHeight.HasValue)
                psd.window_height = windowHeight.Value;
            Properties.Settings.Default.Save();
        }
    }
}
