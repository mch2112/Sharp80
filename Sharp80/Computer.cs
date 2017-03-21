/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;

namespace Sharp80
{
    internal class Computer : IDisposable
    {
        public const ulong CLOCK_RATE = 2027520;

        private const int SERIALIZATION_VERSION = 8;

        public bool Ready { get; private set; }
        public bool HasRunYet { get; private set; }

        private Processor.Z80 Processor { get; set; }
        private Clock Clock { get; set; }
        private FloppyController FloppyController { get; set; }
        private PortSet Ports { get; set; }
        private InterruptManager IntMgr { get; set; }
        private IScreen Screen { get; set; }
        private ISound Sound { get; set; }
        private Tape Tape { get; set; }
        private Printer Printer { get; set; }
        private bool isDisposed = false;

        // CONSTRUCTOR

        public Computer(IAppWindow MainForm, IScreen Screen, ulong DisplayRefreshRateInHz, bool FloppyEnabled, bool NormalSpeed, bool SoundOn)
        {
            ulong milliTStatesPerIRQ = CLOCK_RATE * Clock.TICKS_PER_TSTATE * 100 / 3001; // near 30 hz but not exactly since we don't want to sync with Floppy disk angle
            ulong milliTStatesPerSoundSample = CLOCK_RATE * Clock.TICKS_PER_TSTATE / SoundX.SAMPLE_RATE;

            HasRunYet = false;

            this.Screen = Screen;

            IntMgr = new InterruptManager(this);
            Tape = new Tape(this);
            Ports = new PortSet(this);
            Processor = new Processor.Z80(this, Ports);
            Printer = new Printer();

            // If sound fails to initialize there might be a driver issue,
            // but it's not fatal: we can continue without sound
            Sound = new SoundX(new GetSampleCallback(Ports.CassetteOut));
            if (Sound.Stopped)
            {
                Sound.Dispose();
                Sound = new SoundNull();
            }
            Sound.On = SoundOn;

            Clock = new Clock(this,
                              Processor,
                              IntMgr,
                              CLOCK_RATE,
                              milliTStatesPerIRQ,
                              milliTStatesPerSoundSample,
                              new SoundEventCallback(Sound.Sample),
                              NormalSpeed);

            Clock.SpeedChanged += (s, e) => { Sound.Mute = !Clock.NormalSpeed; };

            FloppyController = new FloppyController(this, Ports, Clock, IntMgr, Sound, FloppyEnabled);

            IntMgr.Initialize(Ports, Tape);
            Tape.Initialize(Clock, IntMgr);
            Ports.Initialize(FloppyController, IntMgr, Tape, Printer);

            Ready = true;
        }

        // PROPERTIES

        /// <summary>
        /// This may vary from Settings.DiskEnabled because we'll disable
        /// the floppy contoller on start if no disk is in drive 0
        /// </summary>
        public bool DiskEnabled
        {
            get => FloppyController.Enabled;
        }
        public bool IsRunning
        {
            get => Clock.IsRunning;
        }
        public ushort ProgramCounter
        {
            get => Processor.PcVal;
        }
        public ulong GetElapsedTStates()
        {
            return Clock.ElapsedTStates;
        }
        public IMemory Memory
        {
            get => Processor.Memory;
        }
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
            get => Memory.AltKeyboardLayout;
            set => Memory.AltKeyboardLayout = value;
        }
        public bool SoundOn
        {
            get => Sound.On;
            set => Sound.On = value;
        }
        public bool DriveNoise
        {
            get => Sound.UseDriveNoise;
            set => Sound.UseDriveNoise = value;
        }
        public IZ80_Status CpuStatus
        {
            // Safe to send this out in interface form
            get => Processor;
        }

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
        public void SaveFloppy(byte DriveNum) => FloppyController.SaveFloppy(DriveNum);

        // RUN COMMANDS

        public void Start()
        {
            if (!HasRunYet)
                if (!FloppyController.Available)
                    FloppyController.Disable();

            HasRunYet = true;

            Clock.Start();
            Sound.Mute = !Clock.NormalSpeed;
        }
        public void Stop(bool WaitForStop)
        {
            Sound.Mute = true;
            Clock.Stop();
            if (WaitForStop)
            {
                while (Clock.IsRunning)
                    System.Threading.Thread.Sleep(0);     // make sure we're not in the middle of a cycle
            }
        }
        public void ResetButton() => IntMgr.ResetButtonLatch.Latch();

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
            {
                Start();
                Stop(true);
            }
            else
            {
                Clock.Step();
            }
        }
        public void Jump(ushort Address)
        {
            Stop(true);
            Processor.Jump(Address);
        }
        public bool NormalSpeed
        {
            get { return Clock.NormalSpeed; }
            set { Clock.NormalSpeed = value; }
        }
        public void Reset()
        {
            if (Ready)
            {
                ResetButton();
                Screen.Reset();
            }
        }
        public void SetVideoMode(bool? Wide, bool? Kanji)
        {
            Screen.SetVideoMode(Wide, Kanji);
        }

        // CALLBACK MANAGEMENT

        /// <summary>
        /// Adds a pulse req and also sets the trigger based on the 
        /// trigger's delay
        /// </summary>
        /// <param name="Req"></param>
        public void Activate(PulseReq Req) => Clock.ActivatePulseReq(Req);

        /// <summary>
        /// Adds a pulse req without resetting the trigger
        /// </summary>
        /// <param name="Req"></param>
        public void AddPulseReq(PulseReq Req) => Clock.AddPulseReq(Req);

        // FLOPPY SUPPORT

        public bool FloppyEnabled { get; set; }
        public void StartupInitializeStorage()
        {
            for (byte i = 0; i < 4; i++)
                LoadFloppy(i);

            var tape = Settings.LastTapeFile;
            if (tape.Length > 0 && File.Exists(tape))
                TapeLoad(tape);
        }
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
                    LoadFloppy(DriveNum, Storage.MakeBlankFloppy(true));
                    ret = true;
                    break;
                case Storage.FILE_NAME_UNFORMATTED:
                    LoadFloppy(DriveNum, Storage.MakeBlankFloppy(false));
                    ret = true;
                    break;
                case "":
                    FloppyController.UnloadDrive(DriveNum);
                    ret = true;
                    break;
                default:
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

        public bool TapeLoad(string Path) { return Tape.Load(Path); }
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

        public Baud TapeUserSelectedSpeed { get => Tape.UserSelectedSpeed; set => Tape.UserSelectedSpeed = value; }

        // PRINTER

        public bool PrinterHasContent => Printer.HasContent;
        public bool PrinterSave() { return Printer.Save(); }
        public string PrinterContent => Printer.PrintBuffer;
        public string PrinterFilePath { get => Printer.FilePath; }
        public void PrinterReset() { Printer.Reset(); }

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
            Clock.Serialize(Writer);
            FloppyController.Serialize(Writer);
            IntMgr.Serialize(Writer);
            Screen.Serialize(Writer);
            Tape.Serialize(Writer);
        }
        private void Deserialize(BinaryReader Reader)
        {
            int ver = Reader.ReadInt32(); // SERIALIZATION_VERSION

            if (ver == SERIALIZATION_VERSION)
            {
                Processor.Deserialize(Reader);
                Clock.Deserialize(Reader);
                FloppyController.Deserialize(Reader);
                IntMgr.Deserialize(Reader);
                Screen.Deserialize(Reader);
                Tape.Deserialize(Reader);
            }
            else
            {
                Dialogs.AlertUser("Snapshot load failed: incompatible snapshot version.");
            }
        }

        // MISC

        public bool LoadCMDFile(CmdFile File)
        {
            Stop(WaitForStop: true);

            if (File.Valid && File.Load(Memory))
            {
                if (File.ExecAddress.HasValue)
                    Processor.Jump(File.ExecAddress.Value);
                return true;
            }
            else
            {
                return false;
            }
        }
        public string Disassemble(bool RelativeAddressesAsComments, bool FromPC)
        {
            return Processor.Disassemble(RelativeAddressesAsComments, FromPC);
        }
        public string GetInstructionSetReport()
        {
            return Processor.GetInstructionSetReport();
        }
        public string Assemble()
        {
            return Processor.Assemble();
        }
        public bool HistoricDisassemblyMode
        {
            get { return Processor.HistoricDisassemblyMode; }
            set { Processor.HistoricDisassemblyMode = value; }
        }
        public string GetInternalsReport()
        {
            return Processor.GetInternalsReport();
        }
        public string GetClockReport()
        {
            return Clock.GetInternalsReport();
        }
        public string GetDisassembly()
        {
            return Processor.GetDisassembly();
        }
        public bool NotifyKeyboardChange(KeyState Key)
        {
            return Processor.Memory.NotifyKeyboardChange(Key);
        }
        public void ResetKeyboard(bool LeftShift, bool RightShift)
        {
            Processor.Memory.ResetKeyboard(LeftShift, RightShift);
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                if (Ready)
                {
                    Stop(true);
                    Ready = false;
                }
                FloppyController.Dispose();
                Sound.Dispose();
                Printer.Dispose();
                Stop(WaitForStop: false);
                isDisposed = true;
            }
        }
    }
}
