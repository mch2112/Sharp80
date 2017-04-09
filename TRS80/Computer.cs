/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    public class Computer : Z80.IComputer
    {
        public const int SERIALIZATION_VERSION = 10;

        private const ushort TAPE_SPEED_SELECT_RAM_LOCATION = 0x4211;

        public bool Ready { get; private set; }
        public bool HasRunYet { get; private set; }

        private Z80.Z80 Processor { get; set; }
        private Clock Clock { get; set; }
        private FloppyController FloppyController { get; set; }
        private PortSet PortSet { get; set; }
        private InterruptManager IntMgr { get; set; }
        private IScreen Screen { get; set; }
        private ISound Sound { get; set; }
        private Tape Tape { get; set; }
        private Printer Printer { get; set; }
        private Memory memory;
        private ISettings Settings { get; set; }
        private IDialogs Dialogs { get; set; }

        // CONSTRUCTOR

        public Computer(IScreen Screen, ISound Sound, ITimer Timer, ISettings Settings, IDialogs Dialogs)
        {
            ulong ticksPerSoundSample = Clock.TICKS_PER_SECOND / (ulong)Sound.SampleRate;

            HasRunYet = false;

            this.Screen = Screen;
            this.Settings = Settings;
            this.Dialogs = Dialogs;
            this.Sound = Sound;

            Storage.Initialize(Settings, Dialogs);
            memory = new Memory();
            IntMgr = new InterruptManager(this);
            Tape = new Tape(this);
            PortSet = new PortSet(this);
            Processor = new Z80.Z80(this);
            Printer = new Printer();

            if (this.Sound.Stopped)
                this.Sound = new SoundNull();
            else
                this.Sound.SampleCallback = PortSet.CassetteOut;

            Clock = new Clock(this,
                              Processor,
                              Timer,
                              IntMgr,
                              ticksPerSoundSample,
                              Sound.Sample);

            Clock.SpeedChanged += (s, e) => Sound.Mute = Clock.ClockSpeed != ClockSpeed.Normal;

            DiskUserEnabled = Settings.DiskEnabled;

            FloppyController = new FloppyController(this, PortSet, Clock, IntMgr, Sound, DiskUserEnabled);

            IntMgr.Initialize(PortSet, Tape);
            Tape.Initialize(Clock, IntMgr);
            PortSet.Initialize(FloppyController, IntMgr, Tape, Printer);

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
        public bool DiskEnabled => FloppyController.Enabled;
        public bool IsRunning => Clock.IsRunning;
        public bool IsStopped => Clock.IsStopped;
        public ushort ProgramCounter => Processor.PcVal;
        public ulong ElapsedTStates => Clock.ElapsedTStates;

        public Z80.IMemory Memory => memory;
        public Z80.IPorts Ports => PortSet;
        public SubArray<byte> VideoMemory => memory.VideoMemory;

        public ushort BreakPoint
        {
            get => Processor.BreakPoint;
            set => Processor.BreakPoint = value;
        }
        public bool BreakPointOn
        {
            get => Processor.BreakPointOn;
            set => Processor.BreakPointOn = value;
        }
        public bool AltKeyboardLayout
        {
            get => memory.AltKeyboardLayout;
            set => memory.AltKeyboardLayout = value;
        }
        public bool SoundOn
        {
            get => Sound.On;
            set => Sound.On = value;
        }
        public bool SoundEnabled => !Sound.Stopped;
        public bool DriveNoise
        {
            get => Sound.UseDriveNoise;
            set => Sound.UseDriveNoise = value;
        }
        public bool DiskUserEnabled { set; get; }
        public Z80.IStatus CpuStatus
        {
            // Safe to send this out in interface form
            get => Processor;
        }

        // FLOPPY SUPPORT

        public IFloppy GetFloppy(byte DriveNum) => FloppyController.GetFloppy(DriveNum);

        public bool DriveIsUnloaded(byte DriveNum) => FloppyController.DriveIsUnloaded(DriveNum);
        public string GetIoStatusReport()
        {
            return FloppyController.StatusReport +
                   (Tape.MotorOn ? " Tape: " + Tape.StatusReport : String.Empty) +
                   (Printer.HasContent ? " PRT" : String.Empty);
        }
        public bool? DriveBusyStatus => FloppyController.DriveBusyStatus;
        public bool AnyDriveLoaded => FloppyController.AnyDriveLoaded;
        public bool FloppyControllerDrq => FloppyController.Drq;
        public string GetFloppyFilePath(byte DriveNum) => FloppyController.FloppyFilePath(DriveNum);
        public void SetFloppyFilePath(byte DriveNum, string Path)
        {
            var f = FloppyController.GetFloppy(DriveNum);
            if (f != null)
                f.FilePath = Path;
        }
        public IFloppyControllerStatus FloppyControllerStatus => FloppyController;
        public bool DiskHasChanged(byte DriveNum) => FloppyController.DiskHasChanged(DriveNum) ?? false;

        public bool SaveChangedStorage()
        {
            return Storage.SaveFloppies(this) && Storage.SaveTapeIfRequired(this);
        }
        public void SaveFloppy(byte DriveNum) => FloppyController.SaveFloppy(DriveNum);

        // RUN AND STEP COMMANDS

        public void Start()
        {
            Init();
            Clock.Start();
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
                if (!DiskUserEnabled || !FloppyController.Available)
                    FloppyController.Disable();

                HasRunYet = true;
            }
            Sound.Mute = Clock.ClockSpeed != ClockSpeed.Normal;
        }
        public void Stop(bool WaitForStop)
        {
            Sound.Mute = true;
            Clock.Stop();
            if (WaitForStop)
            {
                while (!Clock.IsStopped)
                    Thread.Sleep(0);     // make sure we're not in the middle of a cycle
            }
        }
        public async Task StopAndAwait()
        {
            Sound.Mute = true;
            await Clock.StopAndAwait();
        }
        public void ResetButton() => IntMgr.ResetButtonLatch.Latch();
        public void Reset()
        {
            if (Ready)
            {
                ResetButton();
                Screen.Reset();
            }
        }
        public void StepOver()
        {
            if (!IsRunning)
            {
                if (Processor.StepOver())
                    Start();
                else
                    Step();
            }
        }
        public void StepOut()
        {
            if (!IsRunning)
            {
                Processor.SteppedOut = false;
                Start();
            }
        }
        public void Step()
        {
            if (!HasRunYet)
                Init();
            Clock.Step();
        }
        public void Jump(ushort Address)
        {
            Stop(true);
            Processor.Jump(Address);
        }
        
        // CLOCK SPEED

        public ClockSpeed ClockSpeed
        {
            get => Clock.ClockSpeed;
            set => Clock.ClockSpeed = value;
        }
        
        // CHARGEN MODES

        public bool WideCharMode
        {
            get => Screen.WideCharMode;
            set => Screen.WideCharMode = value;
        }
        public bool AltCharMode
        {
            get => Screen.AltCharMode;
            set => Screen.AltCharMode = value;
        }

        // TRACE

        public bool TraceOn
        {
            get => Processor.TraceOn;
            set => Processor.TraceOn = value;
        }
        public string Trace => Processor.Trace;

        // CALLBACK MANAGEMENT

        /// <summary>
        /// Adds a pulse req without resetting the trigger
        /// </summary>
        internal void RegisterPulseReq(PulseReq Req, bool SetTrigger) => Clock.RegisterPulseReq(Req, SetTrigger);

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
                    LoadFloppy(DriveNum, new DMK(true));
                    ret = true;
                    break;
                case Storage.FILE_NAME_UNFORMATTED:
                    LoadFloppy(DriveNum, new DMK(false));
                    ret = true;
                    break;
                case "":
                    FloppyController.UnloadDrive(DriveNum);
                    ret = true;
                    break;
                default:
                    if (FilePath.StartsWith("\\")) // relative to library
                        FilePath = Path.Combine(Storage.LibraryPath, FilePath.Substring(1));
                    ret = FloppyController.LoadFloppy(DriveNum, FilePath);
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
        public void LoadFloppy(byte DriveNum, Floppy Floppy) => FloppyController.LoadFloppy(DriveNum, Floppy);

        public void LoadTrsDosFloppy(byte DriveNum) => LoadFloppy(DriveNum, new DMK(Resources.TRSDOS) { FilePath = Storage.FILE_NAME_TRSDOS });

        public void EjectFloppy(byte DriveNum)
        {
            bool running = IsRunning;

            if (running)
                Stop(WaitForStop: true);

            FloppyController.UnloadDrive(DriveNum);

            Storage.SaveDefaultDriveFileName(DriveNum, String.Empty);

            if (running)
                Start();
        }

        // TAPE DRIVE

        public bool TapeLoad(string Path) => Tape.Load(Path);
        public string TapeFilePath { get => Tape.FilePath; set => Tape.FilePath = value; }
        public void TapeLoadBlank() => Tape.LoadBlank();
        public void TapePlay() => Tape.Play();
        public void TapeRecord() => Tape.Record();
        public void TapeRewind() => Tape.Rewind();
        public void TapeEject() => Tape.Eject();
        public void TapeStop() => Tape.Stop();
        public void TapeSave() => Tape.Save();
        public bool TapeChanged => Tape.Changed;
        public bool TapeMotorOnSignal { set => Tape.MotorOnSignal = value; }
        public bool TapeMotorOn => Tape.MotorOn;
        public float TapePercent => Tape.Percent;
        public float TapeCounter => Tape.Counter;
        public TapeStatus TapeStatus => Tape.Status;
        public bool TapeIsBlank => Tape.IsBlank;
        public string TapePulseStatus => Tape.PulseStatus;
        public Baud TapeSpeed => Tape.Speed;

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

        public bool PrinterHasContent => Printer.HasContent;
        public bool PrinterSave() => Printer.Save();
        public string PrinterContent => Printer.PrintBuffer;
        public string PrinterFilePath => Printer.FilePath;
        public void PrinterReset() => Printer.Reset();

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

            Processor.Serialize(Writer);
            PortSet.Serialize(Writer);
            memory.Serialize(Writer);
            Clock.Serialize(Writer);
            FloppyController.Serialize(Writer);
            IntMgr.Serialize(Writer);
            Screen.Serialize(Writer);
            Tape.Serialize(Writer);
        }
        private bool Deserialize(BinaryReader Reader)
        {
            int ver = Reader.ReadInt32(); // SERIALIZATION_VERSION

            if (ver <= SERIALIZATION_VERSION)
            {
                if (ver >= 8) // currently supporting v 8 & 9
                {
                    if (Processor.Deserialize(Reader, ver) &&
                        PortSet.Deserialize(Reader, ver) &&
                        memory.Deserialize(Reader, ver) &&
                        Clock.Deserialize(Reader, ver) &&
                        FloppyController.Deserialize(Reader, ver) &&
                        IntMgr.Deserialize(Reader, ver) &&
                        Screen.Deserialize(Reader, ver) &&
                        Tape.Deserialize(Reader, ver))
                    {
                        // ok
                        return true;
                    }
                }
            }
            return false;
        }

        // MISC

        public string GetInternalsReport() => Processor.GetInternalsReport();
        public string GetClockReport() => Clock.GetInternalsReport();
        public string GetDisassembly() => Processor.GetRealtimeDisassembly();public bool LoadCMDFile(CmdFile File)
        {
            Stop(WaitForStop: true);

            if (File.Valid && File.Load(memory))
            {
                if (File.ExecAddress.HasValue)
                    Processor.Jump(File.ExecAddress.Value);
                else
                    Processor.Jump(File.LowAddress);
                return true;
            }
            else
            {
                return false;
            }
        }

        public string Disassemble(ushort Start, ushort End, Z80.DisassemblyMode Mode) => Processor.Disassemble(Start, End, Mode);
        public string GetInstructionSetReport() => Processor.GetInstructionSetReport();
        public Z80.Assembler.Assembly Assemble(string SourceText) => Processor.Assemble(SourceText);
        public async Task Delay(uint VirtualMSec)
        {
            bool done = false;
            var pr = new PulseReq(PulseReq.DelayBasis.Microseconds, VirtualMSec * 1000, () => { done = true; });
            Clock.RegisterPulseReq(pr, true);
            while (!done && pr.Active && IsRunning)
                await Task.Delay(Math.Max(2, (int)VirtualMSec / 100));
        }
        public bool HistoricDisassemblyMode
        {
            get => Processor.HistoricDisassemblyMode;
            set => Processor.HistoricDisassemblyMode = value;
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
            FloppyController.Shutdown();
            Printer.Shutdown();
            Tape.Shutdown();
            await StopAndAwait();
            await Sound.Shutdown();
        }
    }
}
