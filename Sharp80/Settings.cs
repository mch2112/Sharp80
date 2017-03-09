/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80
{
    internal static class Settings
    {
        private static bool? advancedView;
        private static bool? altKeyboardLayout;
        private static bool? autoStartOnReset;
        private static ushort? breakpoint;
        private static bool? breakpointOn;
        private static string defaultFloppyDirectory;
        private static string disk0Filename;
        private static string disk1Filename;
        private static string disk2Filename;
        private static string disk3Filename;
        private static bool? driveNoise;
        private static bool? greenScreen;
        private static string lastAsmFile;
        private static string lastCmdFile;
        private static string lastTapeFile;
        private static string lastSnapshotFile;
        private static bool? soundOn;
        private static bool? normalSpeed;

        private static bool? fullScreen;
        private static int? windowX;
        private static int? windowY;
        private static int? windowWidth;
        private static int? windowHeight;


        static Settings()
        {
            System.Diagnostics.Debug.Assert(lastAsmFile == null);
            System.Diagnostics.Debug.Assert(lastCmdFile == null);
            System.Diagnostics.Debug.Assert(lastSnapshotFile == null);
        }

        public static bool AdvancedView
        {
            get
            {
                advancedView = advancedView ?? Properties.Settings.Default.advanced_view;
                return advancedView.Value;
            }
            set { advancedView = value; }
        }
        public static bool AltKeyboardLayout
        {
            get
            {
                altKeyboardLayout = altKeyboardLayout ?? Properties.Settings.Default.alt_keyboard_layout;
                return altKeyboardLayout.Value;
            }
            set { altKeyboardLayout = value; }
        }
        public static bool AutoStartOnReset
        {
            get
            {
                autoStartOnReset = autoStartOnReset ?? Properties.Settings.Default.auto_start_on_reset;
                return autoStartOnReset.Value;
            }
            set { autoStartOnReset = value; }
        }
        public static ushort Breakpoint
        {
            get
            {
                breakpoint = breakpoint ?? Properties.Settings.Default.breakpoint;
                return breakpoint.Value;
            }
            set { breakpoint = value; }
        }
        public static bool BreakpointOn
        {
            get
            {
                breakpointOn = breakpointOn ?? Properties.Settings.Default.breakpoint_on;
                return breakpointOn.Value;
            }
            set { breakpointOn = value; }
        }
        public static string DefaultFloppyDirectory
        {
            get
            {
                defaultFloppyDirectory = defaultFloppyDirectory ?? Properties.Settings.Default.default_floppy_directory;
                return defaultFloppyDirectory;
            }
            set { defaultFloppyDirectory = value; }
        }
        public static string Disk0Filename
        {
            get
            {
                disk0Filename = disk0Filename ?? Properties.Settings.Default.disk0;
                return disk0Filename;
            }
            set { disk0Filename = value; }
        }
        public static string Disk1Filename
        {
            get
            {
                disk1Filename = disk1Filename ?? Properties.Settings.Default.disk1;
                return disk1Filename;
            }
            set { disk1Filename = value; }
        }
        public static string Disk2Filename
        {
            get
            {
                disk2Filename = disk2Filename ?? Properties.Settings.Default.disk2;
                return disk2Filename;
            }
            set { disk2Filename = value; }
        }
        public static string Disk3Filename
        {
            get
            {
                disk3Filename = disk3Filename ?? Properties.Settings.Default.disk3;
                return disk3Filename;
            }
            set { disk3Filename = value; }
        }
        public static bool DriveNoise
        {
            get
            {
                driveNoise = driveNoise ?? Properties.Settings.Default.drive_noise;
                return driveNoise.Value;
            }
            set { driveNoise = value; }
        }
        public static bool GreenScreen
        {
            get
            {
                greenScreen = greenScreen ?? Properties.Settings.Default.green_screen;
                return greenScreen.Value;
            }
            set { greenScreen = value; }
        }
        public static string LastAsmFile
        {
            get
            {
                lastAsmFile = lastAsmFile ?? Properties.Settings.Default.last_asm_file;
                return lastAsmFile;
            }
            set { lastAsmFile = value; }
        }
        public static string LastCmdFile
        {
            get
            {
                lastCmdFile = lastCmdFile ?? Properties.Settings.Default.last_cmd_file;
                return lastCmdFile;
            }
            set { lastCmdFile = value; }
        }
        public static string LastSnapshotFile
        {
            get
            {
                lastSnapshotFile = lastSnapshotFile ?? Properties.Settings.Default.last_snapshot_file;
                return lastSnapshotFile;
            }
            set { lastSnapshotFile = value; }
        }
        public static string LastTapeFile
        {
            get
            {
                lastTapeFile = lastTapeFile ?? Properties.Settings.Default.last_tape_file;
                return lastTapeFile;
            }
            set { lastTapeFile = value; }
        }
        public static bool SoundOn
        {
            get
            {
                soundOn = soundOn ?? Properties.Settings.Default.sound;
                return soundOn.Value;
            }
            set { soundOn = value; }
        }
        public static bool NormalSpeed
        {
            get
            {
                normalSpeed = normalSpeed ?? Properties.Settings.Default.normal_speed;
                return normalSpeed.Value;
            }
            set { normalSpeed = value; }
        }
        public static bool FullScreen
        {
            get
            {
                fullScreen = fullScreen ?? Properties.Settings.Default.full_screen;
                return fullScreen.Value;
            }
            set { fullScreen = value; }
        }
        public static int WindowX
        {
            get
            {
                windowX = windowX ?? Properties.Settings.Default.window_x;
                return windowX.Value;
            }
            set { windowX = value; }
        }
        public static int WindowY
        {
            get
            {
                windowY = windowY ?? Properties.Settings.Default.window_y;
                return windowY.Value;
            }
            set { windowY = value; }
        }
        public static int WindowWidth
        {
            get
            {
                windowWidth = windowWidth ?? Properties.Settings.Default.window_width;
                return windowWidth.Value;
            }
            set { windowWidth = value; }
        }
        public static int WindowHeight
        {
            get
            {
                windowHeight = windowHeight ?? Properties.Settings.Default.window_height;
                return windowHeight.Value;
            }
            set { windowHeight = value; }
        }
        public static void Save()
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
                psd.last_tape_file = lastCmdFile;
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
