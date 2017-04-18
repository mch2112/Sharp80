/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace Sharp80.TRS80
{
    public class Computer : Z80.IComputer
    {
        public const int SERIALIZATION_VERSION = 10;

        private const ushort TAPE_SPEED_SELECT_RAM_LOCATION = 0x4211;

        public bool Ready { get; private set; }
        public bool HasRunYet { get; private set; }

        private Z80.Z80 cpu;
        private Clock clock;
        private FloppyController floppyController;
        private PortSet ports;
        private InterruptManager intMgr;
        private IScreen screen;
        private ISound sound;
        private Tape tape;
        private Printer printer;
        private Memory memory;
        private ISettings settings;
        private IDialogs dialogs;

        // CONSTRUCTOR

        public Computer(IScreen Screen, ISound Sound, ITimer Timer, ISettings Settings, IDialogs Dialogs)
        {
            ulong ticksPerSoundSample = Clock.TICKS_PER_SECOND / (ulong)Sound.SampleRate;

            HasRunYet = false;

            screen = Screen;
            settings = Settings;
            dialogs = Dialogs;
            sound = Sound;

            Storage.Initialize(settings, dialogs);

            memory = new Memory();
            intMgr = new InterruptManager(this);
            this.tape = new Tape(this);
            ports = new PortSet(this);
            cpu = new Z80.Z80(this);
            printer = new Printer();

            if (sound.Stopped)
                sound = new SoundNull();
            else
                sound.SampleCallback = ports.CassetteOut;

            clock = new Clock(this,
                              cpu,
                              Timer,
                              intMgr,
                              ticksPerSoundSample,
                              Sound.Sample);

            clock.SpeedChanged += () => Sound.Mute = clock.ClockSpeed != ClockSpeed.Normal;

            DiskUserEnabled = Settings.DiskEnabled;

            floppyController = new FloppyController(this, ports, clock, intMgr, Sound, DiskUserEnabled);

            intMgr.Initialize(ports, this.tape);
            this.tape.Initialize(clock, intMgr);
            ports.Initialize(floppyController, intMgr, this.tape, printer);

            for (byte i = 0; i < 4; i++)
                LoadFloppy(i);

            var tape = Storage.LastTapeFile;
            if (tape.Length > 0 && File.Exists(tape))
                TapeLoad(tape);

            DriveNoise = Settings.DriveNoise;
            BreakPoint = Settings.Breakpoint;
            BreakPointOn = Settings.BreakpointOn;
            SoundOn = Settings.SoundOn;
            ClockSpeed = Settings.ClockSpeed;
            Ready = true;
        }

        // PROPERTIES

        /// <summary>
        /// This may vary from Settings.DiskEnabled because we'll disable
        /// the floppy contoller on start if no disk is in drive 0
        /// </summary>
        public bool DiskEnabled => floppyController.Enabled;
        public bool IsRunning => clock.IsRunning;
        public bool IsStopped => clock.IsStopped;
        public ushort ProgramCounter => cpu.PcVal;
        public ulong ElapsedTStates => clock.ElapsedTStates;

        public Z80.IMemory Memory => memory;
        public Z80.IPorts Ports => ports;
        public SubArray<byte> VideoMemory => memory.VideoMemory;

        public ushort BreakPoint
        {
            get => cpu.BreakPoint;
            set => cpu.BreakPoint = value;
        }
        public bool BreakPointOn
        {
            get => cpu.BreakPointOn;
            set => cpu.BreakPointOn = value;
        }
        public bool AltKeyboardLayout
        {
            get => memory.AltKeyboardLayout;
            set => memory.AltKeyboardLayout = value;
        }
        public bool SoundOn
        {
            get => sound.On;
            set => sound.On = value;
        }
        public bool SoundEnabled => !sound.Stopped;
        public bool DriveNoise
        {
            get => sound.UseDriveNoise;
            set => sound.UseDriveNoise = value;
        }
        public bool DiskUserEnabled { set; get; }
        public Z80.IStatus CpuStatus
        {
            // Safe to send this out in interface form
            get => cpu;
        }

        // FLOPPY SUPPORT

        public IFloppy GetFloppy(byte DriveNum) => floppyController.GetFloppy(DriveNum);

        public bool DriveIsUnloaded(byte DriveNum) => floppyController.DriveIsUnloaded(DriveNum);
        public string GetIoStatusReport()
        {
            return floppyController.StatusReport +
                   (tape.MotorOn ? " Tape: " + tape.StatusReport : String.Empty) +
                   (printer.HasContent ? " PRT" : String.Empty);
        }
        public bool? DriveBusyStatus => floppyController.DriveBusyStatus;
        public bool AnyDriveLoaded => floppyController.AnyDriveLoaded;
        public bool FloppyControllerDrq => floppyController.Drq;
        public string GetFloppyFilePath(byte DriveNum) => floppyController.FloppyFilePath(DriveNum);
        public void SetFloppyFilePath(byte DriveNum, string Path)
        {
            var f = floppyController.GetFloppy(DriveNum);
            if (f != null)
                f.FilePath = Path;
        }
        public IFloppyControllerStatus FloppyControllerStatus => floppyController;
        public bool DiskHasChanged(byte DriveNum) => floppyController.DiskHasChanged(DriveNum) ?? false;

        public bool SaveChangedStorage()
        {
            return Storage.SaveFloppies(this) && Storage.SaveTapeIfRequired(this);
        }
        public void SaveFloppy(byte DriveNum) => floppyController.SaveFloppy(DriveNum);

        // RUN AND STEP COMMANDS

        public void Start()
        {
            Init();
            clock.Start();
        }
        public async Task StartAndAwait()
        {
            Start();
            while (!IsRunning)
                await Task.Delay(1);
        }
        private void Init()
        {
            if (!HasRunYet)
            {
                if (!DiskUserEnabled || !floppyController.Available)
                    floppyController.Disable();

                HasRunYet = true;
            }
            sound.Mute = clock.ClockSpeed != ClockSpeed.Normal;
        }
        public void Stop(bool WaitForStop)
        {
            sound.Mute = true;
            clock.Stop();
            if (WaitForStop)
            {
                while (!clock.IsStopped)
                    Thread.Sleep(0);     // make sure we're not in the middle of a cycle
            }
        }
        public async Task StopAndAwait()
        {
            sound.Mute = true;
            await clock.StopAndAwait();
        }
        public void ResetButton() => intMgr.ResetButtonLatch.Latch();
        public void Reset()
        {
            if (Ready)
            {
                ResetButton();
                screen.Reset();
            }
        }
        public void StepOver()
        {
            if (!IsRunning)
            {
                if (cpu.StepOver())
                    Start();
                else
                    Step();
            }
        }
        public void StepOut()
        {
            if (!IsRunning)
            {
                cpu.SteppedOut = false;
                Start();
            }
        }
        public void Step()
        {
            if (!HasRunYet)
                Init();
            clock.Step();
        }
        public void Jump(ushort Address)
        {
            Stop(true);
            cpu.Jump(Address);
        }
        
        // CLOCK SPEED

        public ClockSpeed ClockSpeed
        {
            get => clock.ClockSpeed;
            set => clock.ClockSpeed = value;
        }
        
        // CHARGEN MODES

        public bool WideCharMode
        {
            get => screen.WideCharMode;
            set => screen.WideCharMode = value;
        }
        public bool AltCharMode
        {
            get => screen.AltCharMode;
            set => screen.AltCharMode = value;
        }

        // TRACE

        public bool TraceOn
        {
            get => cpu.TraceOn;
            set => cpu.TraceOn = value;
        }
        public string Trace => cpu.Trace;

        // CALLBACK MANAGEMENT

        /// <summary>
        /// Adds a pulse req without resetting the trigger
        /// </summary>
        internal void RegisterPulseReq(PulseReq Req, bool SetTrigger) => clock.RegisterPulseReq(Req, SetTrigger);

        // FLOPPY SUPPORT

        public void LoadFloppy(byte DriveNum) => LoadFloppy(DriveNum, Storage.GetDefaultDriveFileName(DriveNum));
        public bool LoadFloppy(byte DriveNum, string FilePath)
        {
            bool running = IsRunning;
            bool ret = false;

            if (running)
                Stop(WaitForStop: true);

            switch (FilePath)
            {
                case Storage.FILE_NAME_TRSDOS:
                    LoadTrsDosFloppy(DriveNum);
                    ret = true;
                    break;
                case Storage.FILE_NAME_NEW:
                    LoadFloppy(DriveNum, new Floppy(true));
                    ret = true;
                    break;
                case Storage.FILE_NAME_UNFORMATTED:
                    LoadFloppy(DriveNum, new Floppy(false));
                    ret = true;
                    break;
                case "":
                    floppyController.UnloadDrive(DriveNum);
                    ret = true;
                    break;
                default:
                    if (FilePath.StartsWith("\\")) // relative to library
                        FilePath = Path.Combine(Storage.LibraryPath, FilePath.Substring(1));
                    ret = floppyController.LoadFloppy(DriveNum, FilePath);
                    break;
            }
            if (ret)
                Storage.SaveDefaultDriveFileName(DriveNum, FilePath);
            else if (Storage.GetDefaultDriveFileName(DriveNum) == FilePath)
                Storage.SaveDefaultDriveFileName(DriveNum, String.Empty);

            if (running)
                Start();

            return ret;
        }
        public void LoadFloppy(byte DriveNum, Floppy Floppy) => floppyController.LoadFloppy(DriveNum, Floppy);

        public void LoadTrsDosFloppy(byte DriveNum) => LoadFloppy(DriveNum, new Floppy(Resources.TRSDOS, Storage.FILE_NAME_TRSDOS));

        public void EjectFloppy(byte DriveNum)
        {
            bool running = IsRunning;

            if (running)
                Stop(WaitForStop: true);

            floppyController.UnloadDrive(DriveNum);

            Storage.SaveDefaultDriveFileName(DriveNum, String.Empty);

            if (running)
                Start();
        }

        // TAPE DRIVE

        public bool TapeLoad(string Path) => tape.Load(Path);
        public string TapeFilePath { get => tape.FilePath; set => tape.FilePath = value; }
        public void TapeLoadBlank() => tape.LoadBlank();
        public void TapePlay() => tape.Play();
        public void TapeRecord() => tape.Record();
        public void TapeRewind() => tape.Rewind();
        public void TapeEject() => tape.Eject();
        public void TapeStop() => tape.Stop();
        public void TapeSave() => tape.Save();
        public bool TapeChanged => tape.Changed;
        public bool TapeMotorOnSignal { set => tape.MotorOnSignal = value; }
        public bool TapeMotorOn => tape.MotorOn;
        public float TapePercent => tape.Percent;
        public float TapeCounter => tape.Counter;
        public TapeStatus TapeStatus => tape.Status;
        public bool TapeIsBlank => tape.IsBlank;
        public string TapePulseStatus => tape.PulseStatus;
        public Baud TapeSpeed => tape.Speed;
        public int TapeLength => tape.Length;

        /// <summary>
        /// Backdoor to get or change the initial user selection at
        /// the "Cass?" prompt
        /// </summary>
        public Baud TapeUserSelectedSpeed
        {
            get => memory[TAPE_SPEED_SELECT_RAM_LOCATION] == 0x00 ? Baud.Low : Baud.High;
            set => memory[TAPE_SPEED_SELECT_RAM_LOCATION] = (value == Baud.High ? (byte)0xFF : (byte)0x00);
        }

        // PRINTER

        public bool PrinterHasContent => printer.HasContent;
        public string PrinterContent => printer.PrintBuffer;
        public void PrinterReset() => printer.Reset();

        // SNAPSHOTS

        public void SaveSnapshotFile(string FilePath)
        {
            bool running = IsRunning;

            Stop(WaitForStop: true);

            using (BinaryWriter writer = new BinaryWriter(File.Open(FilePath, FileMode.Create)))
            {
                Serialize(writer);
            }

            if (running)
                Start();
        }
        public void LoadSnapshotFile(string FilePath)
        {
            bool running = IsRunning;

            Stop(WaitForStop: true);

            using (BinaryReader reader = new BinaryReader(File.Open(FilePath, FileMode.Open, FileAccess.Read)))
            {
                Deserialize(reader);
            }

            if (running)
                Start();
        }
        private void Serialize(BinaryWriter Writer)
        {
            Writer.Write(SERIALIZATION_VERSION);

            cpu.Serialize(Writer);
            ports.Serialize(Writer);
            memory.Serialize(Writer);
            clock.Serialize(Writer);
            floppyController.Serialize(Writer);
            intMgr.Serialize(Writer);
            screen.Serialize(Writer);
            tape.Serialize(Writer);
        }
        private bool Deserialize(BinaryReader Reader)
        {
            int ver = Reader.ReadInt32(); // SERIALIZATION_VERSION

            if (ver <= SERIALIZATION_VERSION)
            {
                if (ver >= 8) // currently supporting v 8 & 9
                {
                    if (cpu.Deserialize(Reader, ver) &&
                        ports.Deserialize(Reader, ver) &&
                        memory.Deserialize(Reader, ver) &&
                        clock.Deserialize(Reader, ver) &&
                        floppyController.Deserialize(Reader, ver) &&
                        intMgr.Deserialize(Reader, ver) &&
                        screen.Deserialize(Reader, ver) &&
                        tape.Deserialize(Reader, ver))
                    {
                        // ok
                        return true;
                    }
                }
            }
            return false;
        }

        // MISC

        public string ScreenText
        {
            get
            {
                var m = VideoMemory;
                StringBuilder sb = new StringBuilder();
                var inc = WideCharMode ? 2 : 1;
                for (int i = 0; i < ScreenMetrics.NUM_SCREEN_CHARS_Y; i++)
                {
                    for (int j = 0; j < ScreenMetrics.NUM_SCREEN_CHARS_X; j += inc)
                    {
                        var b = m[i * ScreenMetrics.NUM_SCREEN_CHARS_X + j];

                        if (b.IsBetween(0x21, 0x7F))
                        {
                            sb.Append((char)b);
                        }
                        else if (b >= 0x80)
                        {
                            sb.Append('.');
                        }
                        else
                        {
                            sb.Append(' ');
                        }
                    }
                    sb.Append(Environment.NewLine);
                }
                return sb.ToString();
            }
        }
        public string GetInternalsReport() => cpu.GetInternalsReport();
        public string GetClockReport() => clock.GetInternalsReport();
        public string GetDisassembly() => cpu.GetRealtimeDisassembly();public bool LoadCMDFile(CmdFile File)
        {
            Stop(WaitForStop: true);

            if (File.Valid && File.Load(memory))
            {
                if (File.ExecAddress.HasValue)
                    cpu.Jump(File.ExecAddress.Value);
                else
                    cpu.Jump(File.LowAddress);
                return true;
            }
            else
            {
                return false;
            }
        }

        public string Disassemble(ushort Start, ushort End, Z80.DisassemblyMode Mode) => cpu.Disassemble(Start, End, Mode);
        public string GetInstructionSetReport() => cpu.GetInstructionSetReport();
        public Z80.Assembler.Assembly Assemble(string SourceText) => cpu.Assemble(SourceText);
        public async Task Delay(uint VirtualMSec)
        {
            bool done = false;
            var pr = new PulseReq(PulseReq.DelayBasis.Microseconds, VirtualMSec * 1000, () => { done = true; });
            clock.RegisterPulseReq(pr, true);
            while (!done && pr.Active && IsRunning)
                await Task.Delay(Math.Max(2, (int)VirtualMSec / 100));
        }
        public bool HistoricDisassemblyMode
        {
            get => cpu.HistoricDisassemblyMode;
            set => cpu.HistoricDisassemblyMode = value;
        }


        // KEYBOARD

        public bool NotifyKeyboardChange(KeyState Key) => memory.NotifyKeyboardChange(Key);
        public void ResetKeyboard(bool LeftShift, bool RightShift) => memory.ResetKeyboard(LeftShift, RightShift);

        public async Task Paste(string text, CancellationToken Token)
        {
            if (IsRunning)
            {
                foreach (char c in text)
                {
                    var kc = c.ToKeyCode();

                    if (kc.Shifted)
                        NotifyKeyboardChange(new KeyState(KeyCode.LeftShift, true, false, false, true));

                    if (c == '\n')
                        await KeyStroke(kc.Code, kc.Shifted, 1000);
                    else
                        await KeyStroke(kc.Code, kc.Shifted);

                    if (kc.Shifted)
                        NotifyKeyboardChange(new KeyState(KeyCode.LeftShift, true, false, false, false));

                    if (Token.IsCancellationRequested || !IsRunning)
                        break;
                }
            }
        }
        public async Task KeyStroke(KeyCode Key, bool Shifted, uint DelayMSecUp = 70u, uint DelayMSecDown = 70u)
        {
            if (Key != KeyCode.None)
            {
                NotifyKeyboardChange(new KeyState(Key, Shifted, false, false, true));
                await Delay(DelayMSecDown);
                NotifyKeyboardChange(new KeyState(Key, Shifted, false, false, false));
                await Delay(DelayMSecUp);
            }
        }

        // SHUTDOWN

        public async Task Shutdown()
        {
            floppyController.Shutdown();
            tape.Shutdown();
            await StopAndAwait();
            await sound.Shutdown();
        }
    }
}
