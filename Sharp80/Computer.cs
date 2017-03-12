/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;

namespace Sharp80
{
    internal class Computer : IDisposable
    {
        public const ulong CLOCK_RATE = 2027520;

        private const int SERIALIZATION_VERSION = 6;

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

        private bool ready;
        private bool isDisposed = false;

        // CONSTRUCTOR

        public Computer(IAppWindow MainForm, IScreen Screen, ulong DisplayRefreshRateInHz, bool FloppyEnabled, bool NormalSpeed, bool SoundOn)
        {
            ulong milliTStatesPerIRQ = CLOCK_RATE * Clock.TICKS_PER_TSTATE / 30;
            ulong milliTStatesPerSoundSample = CLOCK_RATE * Clock.TICKS_PER_TSTATE / SoundX.SAMPLE_RATE;

            HasRunYet = false;

            this.Screen = Screen;

            IntMgr = new InterruptManager(this);
            Tape = new Tape(this);
            Ports = new PortSet(this);
            Processor = new Processor.Z80(this, Ports);
            Printer = new Printer();

            Sound = new SoundX(new GetSampleCallback(Ports.CassetteOut));
            if (!Sound.Initialized)
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

            ready = true;
        }

        // PROPERTIES

        public bool Ready
        {
            get { return ready; }
        }

        /// <summary>
        /// This may vary from Settings.DiskEnabled because we'll disable
        /// the floppy contoller on start if no disk is in drive 0
        /// </summary>
        public bool DiskEnabled
        {
            get { return FloppyController.Enabled; }
        }
        public bool IsRunning
        {
            get { return Clock.IsRunning; }
        }
        public ushort ProgramCounter
        {
            get { return Processor.PcVal; }
        }
        public ulong GetElapsedTStates()
        {
            return Clock.ElapsedTStates;
        }
        public IMemory Memory
        {
            get { return Processor.Memory; }
        }
        public ushort BreakPoint
        {
            get { return Processor.BreakPoint; }
            set { Processor.BreakPoint = value; }
        }
        public bool BreakPointOn
        {
            get { return Processor.BreakPointOn; }
            set { Processor.BreakPointOn = value; }
        }
        public bool AltKeyboardLayout
        {
            get { return Memory.AltKeyboardLayout; }
            set { Memory.AltKeyboardLayout = value; }
        }
        public bool SoundOn
        {
            get { return Sound.On; }
            set { Sound.On = value; }
        }
        public bool DriveNoise
        {
            get { return Sound.UseDriveNoise; }
            set { Sound.UseDriveNoise = value; }
        }
        public IZ80_Status CpuStatus
        {
            // Safe to send this out in interface form
            get { return Processor; }
        }
        public IFloppy GetFloppy(byte DriveNum) { return FloppyController.GetFloppy(DriveNum); }

        public bool DriveIsUnloaded(byte DriveNum) { return FloppyController.DriveIsUnloaded(DriveNum); }
        public string GetIoStatusReport()
        {
            return FloppyController.StatusReport + (Tape.MotorOn ? " Tape: " + Tape.StatusReport : String.Empty);
        }
        public bool? DriveBusyStatus { get { return FloppyController.DriveBusyStatus; } }
        public bool AnyDriveLoaded { get { return FloppyController.AnyDriveLoaded; } }
        public bool FloppyControllerDrq { get { return FloppyController.DRQ; } }
        public string GetFloppyFilePath(byte DriveNum) { return FloppyController.FloppyFilePath(DriveNum); }
        public void SetFloppyFilePath(byte DriveNum, string Path)
        {
            var f = FloppyController.GetFloppy(DriveNum);
            if (f != null)
                f.FilePath = Path;
        }
        public IFloppyControllerStatus FloppyControllerStatus { get { return FloppyController; } }
        public bool DiskHasChanged(byte DriveNum) { return FloppyController.DiskHasChanged(DriveNum) ?? false; }
        public void SaveFloppy(byte DriveNum) { FloppyController.SaveFloppy(DriveNum); }

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
        public void ResetButton()
        {
            IntMgr.ResetButtonLatch.Latch();
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

        /// <summary>
        /// Adds a pulse req and also sets the trigger based on the 
        /// trigger's delay
        /// </summary>
        /// <param name="Req"></param>
        public void RegisterPulseReq(PulseReq Req)
        {
            Clock.RegisterPulseReq(Req);
        }
        /// <summary>
        /// Adds a pulse req without resetting the trigger
        /// </summary>
        /// <param name="Req"></param>
        public void AddPulseReq(PulseReq Req)
        {
            Clock.AddPulseReq(Req);
        }

        // FLOPPY SUPPORT

        public bool FloppyEnabled { get; set; }
        public void StartupLoadFloppies()
        {
            for (byte i = 0; i < 4; i++)
                LoadFloppy(i);
        }
        public void LoadFloppy(byte DriveNum)
        {
            LoadFloppy(DriveNum, Storage.GetDefaultDriveFileName(DriveNum));
        }
        public void LoadFloppy(byte DriveNum, string FilePath)
        {
            bool running = IsRunning;

            if (running)
                Stop(WaitForStop: true);

            switch (FilePath)
            {
                case Storage.FILE_NAME_TRSDOS:
                    LoadTrsDosFloppy(DriveNum);
                    break;
                case Storage.FILE_NAME_NEW:
                    LoadFloppy(DriveNum, Storage.MakeBlankFloppy(true));
                    break;
                case Storage.FILE_NAME_UNFORMATTED:
                    LoadFloppy(DriveNum, Storage.MakeBlankFloppy(false));
                    break;
                case "":
                    FloppyController.UnloadDrive(DriveNum);
                    break;
                default:
                    FloppyController.LoadFloppy(DriveNum, FilePath);
                    break;
            }

            Storage.SaveDefaultDriveFileName(DriveNum, FilePath);

            if (running)
                Start();
        }
        public void LoadFloppy(byte DriveNum, Floppy Floppy)
        {
            FloppyController.LoadFloppy(DriveNum, Floppy);
        }
        public void LoadTrsDosFloppy(byte DriveNum)
        {
            LoadFloppy(DriveNum, new DMK(Resources.TRSDOS) { FilePath = Storage.FILE_NAME_TRSDOS });
            Storage.SaveDefaultDriveFileName(DriveNum, Storage.FILE_NAME_TRSDOS);
        }
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
        public string TapeFilePath { get { return Tape.FilePath; } set { Tape.FilePath = value; } }
        public void TapeLoadBlank() { Tape.LoadBlank(); }
        public void TapePlay() { Tape.Play(); }
        public void TapeRecord() { Tape.Record(); }
        public void TapeRewind() { Tape.Rewind(); }
        public void TapeEject() { Tape.Eject(); }
        public void TapeStop() { Tape.Stop(); }
        public void TapeSave()  { Tape.Save(); }
        public bool TapeChanged { get { return Tape.Changed; } }
        public bool TapeMotorOnSignal { set { Tape.MotorOnSignal = value; } }
        public bool TapeMotorOn { get { return Tape.MotorOn; } }
        public float TapePercent { get { return Tape.Percent; } }
        public float TapeCounter { get { return Tape.Counter; } }
        public TapeStatus TapeStatus { get { return Tape.Status; } }
        public bool TapeIsBlank {  get { return Tape.IsBlank; } }
        public string TapePulseStatus {  get { return Tape.PulseStatus; } }
        public Baud TapeSpeed { get { return Tape.Speed; } }

        /// <summary>
        /// Backdoor to get or change the initial user selection at
        /// the "Cass?" prompt
        /// </summary>
        public Baud TapeUserSelectedSpeed
        {
            get
            {
                switch (Memory[0x4211])
                {
                    case 0x00:
                        return Baud.Low;
                    default:
                        return Baud.High;
                }
            }
            set
            {
                 Memory[0x4211] = (value == Baud.High) ? (byte)0x48 : (byte)0x00;
            }
        }

        // PRINTER

        public bool PrinterHasContent {  get { return Printer.HasContent; } }
        public bool PrinterSave() { return Printer.Save(); }
        public string PrinterContent { get { return Printer.PrintBuffer; } }
        public string PrinterFilePath { get { return Printer.FilePath; } }
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

        public bool LoadCMDFile(string filePath)
        {
            Stop(WaitForStop: true);

            var pc = Storage.LoadCMDFile(filePath, Processor.Memory);

            if (pc.HasValue)
            {
                Processor.Jump(pc.Value);
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
        public void ResetKeyboard()
        {
            Processor.Memory.ResetKeyboard();
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                if (Ready)
                {
                    Stop(true);
                    ready = false;
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
