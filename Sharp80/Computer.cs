/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.IO;

namespace Sharp80
{
    internal class Computer : IDisposable
    {
        public const ulong CLOCK_RATE = 2027520;

        private const int SERIALIZATION_VERSION = 4;

        public bool HasRunYet { get; private set; }

        private Processor.Z80 Processor { get; set; }
        private Clock Clock { get; set; }
        private FloppyController FloppyController { get; set; }
        private PortSet Ports { get; set; }
        private InterruptManager IntMgr { get; set; }
        private IScreen Screen { get; set; }
        private ISound Sound { get; set; }
        private Tape Tape { get; set; }

        private bool ready;
        private bool isDisposed = false;

        // CONSTRUCTOR

        public Computer(IAppWindow MainForm, IScreen Screen, ulong DisplayRefreshRateInHz, bool NormalSpeed)
        {
            ulong milliTStatesPerIRQ = CLOCK_RATE * Clock.TICKS_PER_TSTATE / 30;
            ulong milliTStatesPerSoundSample = CLOCK_RATE * Clock.TICKS_PER_TSTATE / SoundX.SAMPLE_RATE;

            HasRunYet = false;

            this.Screen = Screen;

            IntMgr = new InterruptManager(this);
            Tape = new Tape(this);
            Ports = new PortSet(this);
            Processor = new Processor.Z80(this, Ports);

            //Sound = new SoundNull();
            Sound = new SoundX(new GetSampleCallback(Ports.CassetteOut))
            {
                On = Settings.SoundOn
            };
            Clock = new Clock(this,
                              Processor,
                              IntMgr,
                              CLOCK_RATE,
                              milliTStatesPerIRQ,
                              milliTStatesPerSoundSample,
                              new SoundEventCallback(Sound.Sample),
                              NormalSpeed);

            Clock.SpeedChanged += (s, e) => { Sound.Mute = !Clock.NormalSpeed; };

            FloppyController = new FloppyController(this, Ports, Clock, IntMgr, Sound);

            IntMgr.Initialize(Ports, Tape);
            Tape.Initialize(Clock, IntMgr);
            Ports.Initialize(FloppyController, IntMgr, Tape);

            ready = true;
        }

        // PROPERTIES

        public bool Ready
        {
            get { return ready; }
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
        public string GetDriveStatusReport() { return FloppyController.GetDriveStatusReport(); }
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
        public FloppyControllerStatus FloppyControllerStatus { get { return FloppyController.GetStatus(); } }
        public bool DiskHasChanged(byte DriveNum) { return FloppyController.DiskHasChanged(DriveNum) ?? false; }
        public void SaveFloppy(byte DriveNum) { FloppyController.SaveFloppy(DriveNum); }

        // RUN COMMANDS

        public void Start()
        {
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
        public void HardwareReset()
        {
            Stop(WaitForStop: true);
            FloppyController.HardwareReset();
            Ports.Reset();
            Processor.Reset();
            HasRunYet = false;
        }
        public void ShutDown()
        {
            ready = false;
        }
        public void StepOver()
        {
            if (!IsRunning)
            {
                if (Processor.StepOver())
                    Start();
                else
                    SingleStep();
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
        public void SingleStep()
        {
            Clock.SingleStep();
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
        public void RegisterPulseReq(PulseReq Req)
        {
            Clock.RegisterPulseReq(Req);
        }

        // FLOPPY SUPPORT

        public bool HasDrivesAvailable
        {
            get { return !Ports.NoDrives; }
            set { Ports.NoDrives = !value; }
        }
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
                case Floppy.FILE_NAME_TRSDOS:
                    LoadTrsDosFloppy(DriveNum);
                    break;
                case Floppy.FILE_NAME_BLANK:
                    LoadFloppy(DriveNum, Storage.MakeBlankFloppy(true));
                    break;
                case Floppy.FILE_NAME_UNFORMATTED:
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

            if (DriveNum == 0 && !HasRunYet)
                Ports.NoDrives = FloppyController.DriveIsUnloaded(0);

            if (running)
                Start();
        }
        public void LoadFloppy(byte DriveNum, Floppy Floppy)
        {
            FloppyController.LoadFloppy(DriveNum, Floppy);

            if (DriveNum == 0 && !HasRunYet)
                Ports.NoDrives = FloppyController.DriveIsUnloaded(0);
        }
        public void LoadTrsDosFloppy(byte DriveNum)
        {
            LoadFloppy(DriveNum, new DMK(Resources.TRSDOS) { FilePath = Floppy.FILE_NAME_TRSDOS });
            Storage.SaveDefaultDriveFileName(DriveNum, Floppy.FILE_NAME_TRSDOS);
        }
        public void EjectFloppy(byte DriveNum)
        {
            bool running = IsRunning;

            if (running)
                Stop(WaitForStop: true);

            FloppyController.UnloadDrive(DriveNum);
            Storage.SaveDefaultDriveFileName(DriveNum, String.Empty);

            if (DriveNum == 0 && !HasRunYet)
                Ports.NoDrives = FloppyController.DriveIsUnloaded(0);

            if (running)
                Start();
        }

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

        // CASSETTE

        public void TapeLoad(string Path)
        {
            Tape.Load(Path);
        }
        public string TapeFilePath { get { return Tape.FilePath; } }
        
        public void TapeLoadBlank()
        {
            Tape.LoadBlank();
        }
        public void TapeSave()
        {
            Tape.Save();
        }
        public void TapePlay()
        {
            Tape.Play();
        }
        public void TapeRecord()
        {
            Tape.Record();
        }
        public void TapeRewind()
        {
            Tape.Rewind();
        }
        public bool TapeMotorOnSignal
        {
            set { Tape.MotorOnSignal = value; }
        }
        public TimeSpan TapeElapsedTime
        {
            get { return Tape.Time; }
        }
        public float TapeCounter
        {
            get { return Tape.Counter; }
        }
        public TapeStatus TapeStatus { get { return Tape.Status; } }
        public string TapePulseStatus {  get { return Tape.PulseStatus; } }
        public byte TapeValue { get { return (Tape.FlipFlopVal() == 0) ? (byte)0 : (byte)1; } }
        public TapeSpeed TapeSpeed { get { return Tape.Speed; } }

        // MISC

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

            if (ver != SERIALIZATION_VERSION)
                Dialogs.AlertUser("Snapshot load failed: incompatible snapshot version.");

            Processor.Deserialize(Reader);
            Clock.Deserialize(Reader);
            FloppyController.Deserialize(Reader);
            IntMgr.Deserialize(Reader);
            Screen.Deserialize(Reader);
            Tape.Deserialize(Reader);
        }

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
                if (!Sound.IsDisposed)
                    Sound.Dispose();
                
                Stop(WaitForStop: false);
                
                isDisposed = true;
            }
        }
        public bool IsDisposed { get { return isDisposed; } }
    }
}
