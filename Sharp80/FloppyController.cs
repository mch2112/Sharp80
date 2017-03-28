/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Linq;

namespace Sharp80
{
    internal partial class FloppyController : ISerializable, IFloppyControllerStatus, IDisposable
    {
        public const int NUM_DRIVES = 4;

        private enum Command
        { Reset, Restore, Seek, Step, ReadSector, WriteSector, ReadTrack, WriteTrack, ReadAddress, ForceInterrupt, ForceInterruptImmediate, Invalid }
        private enum OpStatus
        { Prepare, Delay, Step, CheckVerify, VerifyTrack, CheckingWriteProtectStatus, SetDrq, DrqCheck, SeekingIndexHole, SeekingIDAM, ReadingAddressData, CheckingAddressData, SeekingDAM, ReadingData, WritingData, WriteFiller, WriteFilter2, WriteDAM, WriteCRCHigh, WriteCRCLow, ReadCRCHigh, ReadCRCLow, Finalize, NMI, OpDone }

        private PortSet ports;
        private InterruptManager IntMgr;
        private Clock clock;
        private Computer computer;
        private ISound sound;

        private const int MAX_TRACKS = 80;                                  // Really should be 76 for 1793
        private const ulong DISK_ANGLE_DIVISIONS = 1000000ul;               // measure millionths of a rotation
        private const ulong SECONDS_TO_MICROSECONDS = 1000000ul;
        private const ulong MILLISECONDS_TO_MICROSECONDS = 1000ul;
        private const ulong DISK_REV_PER_SEC = 300 / 60; // 300 rpm
        private const ulong TRACK_FULL_ROTATION_TIME_IN_USEC = SECONDS_TO_MICROSECONDS / DISK_REV_PER_SEC; // 300 rpm
        private const ulong INDEX_PULSE_WIDTH_IN_USEC = 20 / FDC_CLOCK_MHZ;
        private const ulong INDEX_PULSE_END = 10000;//INDEX_PULSE_WIDTH_IN_USEC * DISK_ANGLE_DIVISIONS / TRACK_FULL_ROTATION_TIME_IN_USEC;
        private const ulong MOTOR_OFF_DELAY_IN_USEC = 2 * SECONDS_TO_MICROSECONDS;
        private const ulong MOTOR_ON_DELAY_IN_USEC = 10;
        private const ulong BYTE_TIME_IN_USEC_DD = 1000000 / Floppy.STANDARD_TRACK_LENGTH_DOUBLE_DENSITY / 5;
        private const ulong BYTE_TIME_IN_USEC_SD = 1000000 / Floppy.STANDARD_TRACK_LENGTH_SINGLE_DENSITY / 5;
        private const ulong BYTE_POLL_TIME_DD = BYTE_TIME_IN_USEC_DD / 10;
        private const ulong BYTE_POLL_TIME_SD = BYTE_TIME_IN_USEC_SD / 10;
        private const ulong STANDARD_DELAY_TIME_IN_USEC = 30 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ;
        private const ulong HEAD_LOAD_TIME_INI_USEC = 50 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ;
        private const ulong FDC_CLOCK_MHZ = 1;
        private const ulong NMI_DELAY_IN_USEC = 30; // ?

        private const int ADDRESS_DATA_TRACK_REGISTER_BYTE = 0x00;
        private const int ADDRESS_DATA_SIDE_ONE_BYTE = 0x01;
        private const int ADDRESS_DATA_SECTOR_REGISTER_BYTE = 0x02;
        private const int ADDRESS_DATA_SECTOR_SIZE_BYTE = 0x03;
        private const int ADDRESS_DATA_CRC_HIGH_BYTE = 0x04;
        private const int ADDRESS_DATA_CRC_LOW_BYTE = 0x05;
        private const int ADDRESS_DATA_BYTES = 0x06;

        public bool Enabled { get; private set; }
        public bool MotorOn { get; private set; }
        private ulong stepRateInUsec;
        private readonly ulong[] stepRates;
        private bool verify;
        private bool delay;
        private bool updateRegisters;
        private bool sideSelectVerify;
        private bool sideOneExpected;
        private bool markSectorDeleted;
        private bool multipleRecords;
        private PulseReq motorOffPulseReq;
        private PulseReq motorOnPulseReq;
        private PulseReq commandPulseReq;

        private Track track;

        // FDC Hardware Registers
        public byte TrackRegister { get; private set; }
        public byte CommandRegister { get; private set; }
        public byte DataRegister { get; private set; }
        public byte SectorRegister { get; private set; }
        private byte statusRegister;

        // FDC Flags, etc.
        public bool Busy { get; private set; }
        public bool DoubleDensitySelected { get; private set; }
        public bool SideOneSelected { get; private set; }
        public bool Drq { get; private set; }
        public bool CrcError { get; private set; }
        public bool LostData { get; private set; }
        public bool SeekError { get; private set; }
        private bool lastStepDirUp;
        private bool sectorDeleted;
        private bool writeProtected;

        // The physical state
        private byte currentDriveNumber = 0xFF;
        public byte CurrentDriveNumber
        {
            get { return currentDriveNumber; }
            set
            {
                if (currentDriveNumber != value)
                {
                    currentDriveNumber = value;
                    UpdateTrack();
                }
            }
        }
        private DriveState[] drives;
        
        // Operation state
        private Command command;
        private OpStatus opStatus = OpStatus.OpDone;
        private byte[] readAddressData = new byte[ADDRESS_DATA_BYTES];
        private byte readAddressIndex;
        private int damBytesChecked;
        private int idamBytesFound;
        private int sectorLength;
        private int bytesRead;
        private ulong indexCheckStartTick;
        private int bytesToWrite;
        private ushort crc;
        private ushort crcCalc;
        private byte crcHigh, crcLow;
        private Clock.ClockCallback nextCallback;
        private bool isPolling;
        private int targetDataIndex;
        private ulong TicksPerDiskRev { get; }

        // CONSTRUCTOR

        public FloppyController(Computer Computer, PortSet Ports, Clock Clock, InterruptManager InterruptManager, ISound Sound, bool Enabled)
        {
            computer = Computer;
            ports = Ports;
            IntMgr = InterruptManager;
            clock = Clock;
            sound = Sound;
            this.Enabled = Enabled;

            TicksPerDiskRev = Clock.TICKS_PER_SECOND / DISK_REV_PER_SEC;

            stepRates = new ulong[4] {  6 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ,
                                       12 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ,
                                       20 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ,
                                       30 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ };

            drives = new DriveState[NUM_DRIVES];
            for (int i = 0; i < NUM_DRIVES; i++)
                drives[i] = new DriveState();

            CurrentDriveNumber = 0x00;

            HardwareReset();

            MotorOn = false;
        }

        // STATUS INFO

        public string OperationStatus => opStatus.ToString();
        public string CommandStatus => command.ToString();
        public byte PhysicalTrackNum => CurrentDrive.PhysicalTrackNumber;
        public string DiskAngleDegrees => ((double)DiskAngle / DISK_ANGLE_DIVISIONS * 360).ToString("000.00000") + " degrees";
        public byte ValueAtTrackDataIndex => track?.ReadByte(TrackDataIndex, null) ?? 0;
        public Floppy GetFloppy(int DriveNumber) => drives[DriveNumber].Floppy;
        private DriveState CurrentDrive => (CurrentDriveNumber >= NUM_DRIVES) ? null : drives[CurrentDriveNumber];

        public static ushort UpdateCRC(ushort crc, byte ByteRead, bool AllowReset, bool DoubleDensity)
        {
            if (AllowReset)
            {
                switch (ByteRead)
                {
                    case 0xF8:
                    case 0xF9:
                    case 0xFA:
                    case 0xFB:
                    case 0xFD:
                    case 0xFE:
                        if (!DoubleDensity)
                            crc = Floppy.CRC_RESET;
                        break;
                    case 0xA1:
                        if (DoubleDensity)
                            crc = Floppy.CRC_RESET_A1_A1;
                        break;
                }
            }
            crc = Lib.Crc(crc, ByteRead);
            return crc;
        }

        // EXTERNAL INTERACTION AND INFORMATION

        public void HardwareReset()
        {
            // This is a hardware reset, not an FDC command
            // We need to set the results as if a Restore command was completed.

            CommandRegister = 0x03;
            DataRegister = 0;
            TrackRegister = 0;
            SectorRegister = 0x01;
            statusRegister = 0;

            command = Command.Restore;

            Busy = false;
            Drq = false;
            SeekError = false;
            LostData = false;
            CrcError = false;
            sectorDeleted = false;
            writeProtected = false;

            lastStepDirUp = true;

            sideOneExpected = false;
            sideSelectVerify = false;
            DoubleDensitySelected = false;

            motorOffPulseReq?.Expire();
            motorOnPulseReq?.Expire();
            commandPulseReq?.Expire();

            motorOnPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, MOTOR_ON_DELAY_IN_USEC, MotorOnCallback, true);
            motorOffPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, MOTOR_OFF_DELAY_IN_USEC, MotorOffCallback, true);
            commandPulseReq = new PulseReq();

            isPolling = false;
        }
        public bool LoadFloppy(byte DriveNum, string FilePath)
        {
            var ret = false;
            if (FilePath.Length == 0)
            {
                UnloadDrive(DriveNum);
                ret = true;
            }
            else
            {
                Floppy f = null;
                try
                {
                    f = Floppy.LoadDisk(FilePath);
                    ret = !(f is null);
                }
                catch (Exception ex)
                {
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, "Failed to load flopy from " + FilePath);
                    ret = false;
                }
                if (f == null)
                    UnloadDrive(DriveNum);
                else
                    LoadDrive(DriveNum, f);
            }
            return ret;
        }
        public void LoadFloppy(byte DriveNum, Floppy Floppy)
        {
            LoadDrive(DriveNum, Floppy);
        }
        public void SaveFloppy(byte DriveNum)
        {
            try
            {
                Floppy f = drives[DriveNum].Floppy;

                if (string.IsNullOrWhiteSpace(f.FilePath))
                    Log.LogDebug("Can't save floppy without file path.");
                else
                    IO.SaveBinaryFile(f.FilePath, f.Serialize(ForceDMK: false));
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"Error saving drive number: {DriveNum}");
            }
        }
        private void LoadDrive(byte DriveNum, Floppy Floppy)
        {
            drives[DriveNum].Floppy = Floppy;
            UpdateTrack();
        }
        public void UnloadDrive(byte DriveNum)
        {
            drives[DriveNum].Floppy = null;
            UpdateTrack();
        }

        public string StatusReport
        {
            get
            {
                return "Dsk: " + CurrentDriveNumber.ToString() +
                       ":S" + (SideOneSelected ? "1" : "0") +
                       ":T" + CurrentDrive.PhysicalTrackNumber.ToString("00") +
                       ":S" + SectorRegister.ToString("00");
            }
        }

        public bool? DriveBusyStatus => MotorOn ? (bool?)Busy : null;
        
        public bool AnyDriveLoaded => drives.Any(d => !d.IsUnloaded);
        public bool Available => Enabled && !DriveIsUnloaded(0);
        public void Disable() => Enabled = false;
        public bool DriveIsUnloaded(byte DriveNum) => drives[DriveNum].IsUnloaded;
        public string FloppyFilePath(byte DriveNum) => drives[DriveNum].Floppy?.FilePath ?? String.Empty;
        public bool? DiskHasChanged(byte DriveNum) => drives[DriveNum].Floppy?.Changed;
        
        public bool? IsWriteProtected(byte DriveNumber)
        {
            if (DriveIsUnloaded(DriveNumber))
                return null;
            else
                return drives[DriveNumber].WriteProtected;
        }
        public void SetWriteProtection(byte DriveNumber, bool WriteProtected)
        {
            if (!DriveIsUnloaded(DriveNumber))
                drives[DriveNumber].WriteProtected = WriteProtected;
        }

        // CALLBACKS

        private void TypeOneCommandCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 0;

            switch (opStatus)
            {
                case OpStatus.Prepare:
                    verify = CommandRegister.IsBitSet(2);
                    stepRateInUsec = stepRates[CommandRegister & 0x03];
                    updateRegisters = command == Command.Seek || command == Command.Restore || CommandRegister.IsBitSet(4);
                    opStatus = OpStatus.Step;
                    break;
                case OpStatus.Step:

                    if (command == Command.Seek || command == Command.Restore)
                    {
                        if (DataRegister == TrackRegister)
                            opStatus = OpStatus.CheckVerify;
                        else
                            lastStepDirUp = (DataRegister > TrackRegister);
                    }
                    if (opStatus == OpStatus.Step)
                    {
                        if (updateRegisters)
                            TrackRegister = lastStepDirUp ? (byte)Math.Min(MAX_TRACKS, TrackRegister + 1)
                                                          : (byte)Math.Max(0, TrackRegister - 1);

                        if (lastStepDirUp)
                            StepUp();
                        else
                            StepDown();

                        if (CurrentDrive.OnTrackZero && !lastStepDirUp)
                        {
                            TrackRegister = 0;
                            opStatus = OpStatus.CheckVerify;
                        }
                        else if (command == Command.Step)
                        {
                            opStatus = OpStatus.CheckVerify;
                        }
                        delayTime = stepRateInUsec;
                    }
                    break;
                case OpStatus.CheckVerify:

                    if (CurrentDrive.PhysicalTrackNumber != TrackRegister)
                        Log.LogDebug($"Track Register {TrackRegister:X2} != Physical Track Number {CurrentDrive.PhysicalTrackNumber:X2}");

                    if (verify)
                    {
                        delayTime = STANDARD_DELAY_TIME_IN_USEC;
                        ResetIndexCount();
                        ResetCRC();
                        opStatus = OpStatus.SeekingIDAM;
                    }
                    else
                    {
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    break;
                case OpStatus.SeekingIDAM:
                case OpStatus.ReadingAddressData:
                    byte b = ReadByte(opStatus == OpStatus.SeekingIDAM);
                    delayBytes = 1;
                    SeekAddressData(b, OpStatus.VerifyTrack, false);
                    break;
                case OpStatus.VerifyTrack:
                    if (TrackRegister == readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE])
                    {
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        Log.LogDebug($"{opStatus} Verify Track failed: Track Register: {TrackRegister:X2} Track Read {readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]:X2}");
                        delayBytes = 1;
                        opStatus = OpStatus.SeekingIDAM;
                    }
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (Busy)
                SetCommandPulseReq(delayBytes, delayTime, TypeOneCommandCallback);
        }
        private void ReadSectorCallback()
        {
            byte b = ReadByte(false);

            ulong delayTime = 0;
            int delayBytes = 0;

            switch (opStatus)
            {
                case OpStatus.Prepare:

                    ResetIndexCount();

                    ResetCRC();

                    sideSelectVerify =CommandRegister.IsBitSet(1);
                    sideOneExpected = CommandRegister.IsBitSet(3);
                    multipleRecords = CommandRegister.IsBitSet(4);

                    if (delay)
                        opStatus = OpStatus.Delay;
                    else
                        opStatus = OpStatus.SeekingIDAM;

                    break;
                case OpStatus.Delay:
                    delayTime = STANDARD_DELAY_TIME_IN_USEC;
                    opStatus = OpStatus.SeekingIDAM;
                    break;
                case OpStatus.SeekingIDAM:
                case OpStatus.ReadingAddressData:
                    SeekAddressData(b, OpStatus.SeekingDAM, true);
                    delayBytes = 1;
                    break;
                case OpStatus.SeekingDAM:
                    delayBytes = 1;
                    if (damBytesChecked++ > (DoubleDensitySelected ? 43 : 30))
                    {
                        Log.LogDebug($"Error: Seek Error / Record Not Found. Command: {command:X2} OpStatus: {opStatus}");
                        SeekError = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                        delayBytes = 0;
                    }
                    else if (IsDAM(b, out sectorDeleted))
                    {
                        Log.LogDebug("Dam Found: " + b.ToHexString());
                        ResetCRC();
                        crc = Lib.Crc(crc, b);
                        opStatus = OpStatus.ReadingData;
                        bytesRead = 0;
                    }
                    else
                    {
                        Log.LogDebug($"Check for DAM, byte {damBytesChecked}");
                    }
                    break;
                case OpStatus.ReadingData:
                    if (Drq)
                    {
                        Log.LogDebug($"Error: Lost Data. Command: {command} OpStatus: {opStatus} TrackRegister: {TrackRegister:X2} SectorRegister {SectorRegister:X2} Bytes Read: {bytesRead} Disk Angle: {DiskAngleDegrees}");
                        LostData = true;
                    }
                    if (++bytesRead >= sectorLength)
                    {
                        crcCalc = crc;
                        opStatus = OpStatus.ReadCRCHigh;
                    }
                    Log.LogDebug($"FDC Write to data register: {b:X2} ({bytesRead} of {sectorLength})");
                    DataRegister = b;
                    SetDRQ();
                    delayBytes = 1;
                    break;
                case OpStatus.ReadCRCHigh:
                    crcHigh = b;
                    delayBytes = 1;
                    opStatus = OpStatus.ReadCRCLow;
                    break;
                case OpStatus.ReadCRCLow:
                    crcLow = b;
                    CrcError = crcCalc != Lib.CombineBytes(crcLow, crcHigh);
                    if (CrcError)
                    {
                        Log.LogDebug($"Error: CRC Error. Command: {command} OpStatus: {opStatus}");
                        Log.LogDebug(string.Format("Data CRC Error - Drv: {0} Trk: {1} Side: {2} Sec: {3:X2}", CurrentDriveNumber, TrackRegister, sideOneExpected ? 1 : 0, SectorRegister));
                        delayTime = NMI_DELAY_IN_USEC;
                        opStatus = OpStatus.NMI;
                    }
                    else if (!multipleRecords)
                    {
                        delayTime = NMI_DELAY_IN_USEC;
                        opStatus = OpStatus.NMI;
                    }
                    else
                    {
                        // mutiple records
                        SectorRegister++;
                        delayBytes = 1;
                        opStatus = OpStatus.SeekingIDAM;
                    }

                    break;
                case OpStatus.NMI:
                    Log.LogDebug($"Read Sector Done OK: Track {TrackRegister:X2} Sector {SectorRegister:X2}");
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (Busy)
                SetCommandPulseReq(delayBytes, delayTime, ReadSectorCallback);
        }
        private void WriteSectorCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 0;

            switch (opStatus)
            {
                case OpStatus.Prepare:

                    ResetIndexCount();

                    crc = 0xFFFF;

                    sideOneExpected =   CommandRegister.IsBitSet(3);
                    delay =             CommandRegister.IsBitSet(2);
                    sideSelectVerify =  CommandRegister.IsBitSet(1);
                    markSectorDeleted = CommandRegister.IsBitSet(0);

                    if (delay)
                        opStatus = OpStatus.Delay;
                    else
                        opStatus = OpStatus.CheckingWriteProtectStatus;

                    delayTime = 0;
                    break;
                case OpStatus.Delay:
                    opStatus = OpStatus.CheckingWriteProtectStatus;
                    delayTime = STANDARD_DELAY_TIME_IN_USEC;
                    break;
                case OpStatus.CheckingWriteProtectStatus:
                    if (CurrentDrive.WriteProtected)
                    {
                        Log.LogDebug($"Cancelling due to Write Protect: Command: {command} OpStatus: {opStatus}");

                        writeProtected = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        opStatus = OpStatus.SeekingIDAM;
                    }
                    break;
                case OpStatus.SeekingIDAM:
                case OpStatus.ReadingAddressData:
                    byte b = ReadByte(true);
                    SeekAddressData(b, OpStatus.SetDrq, true);
                    if (opStatus == OpStatus.SetDrq)
                        delayBytes = 2;
                    else
                        delayBytes = 1;
                    break;
                case OpStatus.SetDrq:
                    ResetCRC();
                    opStatus = OpStatus.DrqCheck;
                    SetDRQ();
                    delayBytes = 8;
                    break;
                case OpStatus.DrqCheck:
                    if (Drq)
                    {
                        Log.LogDebug($"Error: Lost Data. Command: {command} OpStatus: {opStatus}");
                        LostData = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        opStatus = OpStatus.WriteFiller;
                        delayBytes = DoubleDensitySelected ? 12 : 1;
                        bytesToWrite = DoubleDensitySelected ? 12 : 6;
                    }
                    break;
                case OpStatus.WriteFiller:
                    WriteByte(0x00, false);
                    if (--bytesToWrite == 0)
                    {
                        opStatus = OpStatus.WriteDAM;
                        bytesToWrite = 4;
                        crc = Floppy.CRC_RESET;
                    }
                    delayBytes = 1;
                    break;
                case OpStatus.WriteDAM:
                    if (--bytesToWrite > 0)
                    {
                        WriteByte(0xA1, false);
                    }
                    else
                    {
                        opStatus = OpStatus.WritingData;
                        if (markSectorDeleted)
                            WriteByte(Floppy.DAM_DELETED, true);
                        else
                            WriteByte(Floppy.DAM_NORMAL, true);
                        bytesToWrite = sectorLength;
                    }
                    delayBytes = 1;
                    break;
                case OpStatus.WritingData:
                    if (Drq)
                    {
                        Log.LogDebug($"Error: Lost Data. Command: {command} OpStatus: {opStatus}");
                        LostData = true;
                        WriteByte(0x00, false);
                    }
                    else
                    {
                        WriteByte(DataRegister, false);
                    }
                    if (--bytesToWrite == 0)
                    {
                        opStatus = OpStatus.WriteCRCHigh;
                        crc.Split(out crcLow, out crcHigh);
                    }
                    else
                    {
                        SetDRQ();
                    }
                    delayBytes = 1;
                    break;
                case OpStatus.WriteCRCHigh:
                    opStatus = OpStatus.WriteCRCLow;
                    WriteByte(crcHigh, false);
                    delayBytes = 1;
                    break;
                case OpStatus.WriteCRCLow:
                    opStatus = OpStatus.WriteFilter2;
                    WriteByte(crcLow, false);
                    delayBytes = 1;
                    break;
                case OpStatus.WriteFilter2:
                    WriteByte(0xFF, false);
                    if (multipleRecords)
                    {
                        opStatus = OpStatus.SetDrq;
                        delayBytes = 2;
                    }
                    else
                    {
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;

            }
            if (Busy)
                SetCommandPulseReq(delayBytes, delayTime, WriteSectorCallback);
        }
        private void ReadAddressCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 1;

            byte b = ReadByte(true);
            switch (opStatus)
            {
                case OpStatus.Prepare:
                    ResetIndexCount();

                    delay = CommandRegister.IsBitSet(2);

                    if (delay)
                        opStatus = OpStatus.Delay;
                    else
                        opStatus = OpStatus.SeekingIDAM;
                    delayBytes = 0;
                    break;
                case OpStatus.Delay:
                    opStatus = OpStatus.SeekingIDAM;
                    delayTime = STANDARD_DELAY_TIME_IN_USEC;
                    delayBytes = 0;
                    break;
                case OpStatus.SeekingIDAM:
                    SeekAddressData(b, OpStatus.Finalize, false);
                    break;
                case OpStatus.ReadingAddressData:
                    DataRegister = b;
                    SetDRQ();
                    SeekAddressData(b, OpStatus.Finalize, false);
                    break;
                case OpStatus.Finalize:
                    // Error in doc? from the doc: "The Track Address of the ID field is written
                    // into the sector register so that a comparison can be made
                    // by the user"
                    TrackRegister = readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE];
                    SectorRegister = readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE];
                    opStatus = OpStatus.NMI;
                    delayTime = NMI_DELAY_IN_USEC;
                    delayBytes = 0;
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (Busy)
                SetCommandPulseReq(delayBytes, delayTime, ReadAddressCallback);
        }
        private void WriteTrackCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 1;

            switch (opStatus)
            {
                case OpStatus.Prepare:
                    ResetIndexCount();

                    if (delay)
                        opStatus = OpStatus.Delay;
                    else
                        opStatus = OpStatus.CheckingWriteProtectStatus;

                    delayTime = 100;
                    delayBytes = 0;

                    break;
                case OpStatus.Delay:
                    opStatus = OpStatus.CheckingWriteProtectStatus;
                    delayTime = STANDARD_DELAY_TIME_IN_USEC;
                    delayBytes = 0;
                    break;
                case OpStatus.CheckingWriteProtectStatus:
                    if (CurrentDrive.WriteProtected)
                    {
                        Log.LogDebug($"Cancelling due to Write Protect: Command: {command} OpStatus: {opStatus}");

                        writeProtected = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                        delayBytes = 0;
                    }
                    else
                    {
                        opStatus = OpStatus.DrqCheck;
                        SetDRQ();
                        delayBytes = 3;
                    }
                    break;
                case OpStatus.DrqCheck:
                    if (Drq)
                    {
                        Log.LogDebug($"Error: Lost Data. Command: {command} OpStatus: {opStatus}");
                        LostData = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        opStatus = OpStatus.SeekingIndexHole;
                    }
                    delayBytes = 0;
                    break;
                case OpStatus.SeekingIndexHole:
                    if (IndexesFound > 0)
                        opStatus = OpStatus.WritingData;
                    break;
                case OpStatus.WritingData:
                    byte b = DataRegister;
                    bool doDrq = true;
                    bool allowCrcReset = true;
                    if (Drq)
                    {
                        Log.LogDebug($"Error: Lost Data. Command: {command} OpStatus: {opStatus}");
                        LostData = true;
                        b = 0x00;
                    }
                    else
                    {
                        if (DoubleDensitySelected)
                        {
                            switch (b)
                            {
                                case 0xF5:
                                    b = 0xA1;
                                    break;
                                case 0xF6:
                                    b = 0xC2;
                                    break;
                                case 0xF7:
                                    // write 2 bytes CRC
                                    crcCalc = crc;
                                    crc.Split(out crcLow, out crcHigh);
                                    b = crcHigh;
                                    opStatus = OpStatus.WriteCRCLow;
                                    doDrq = false;
                                    allowCrcReset = false;
                                    break;
                            }
                        }
                        else
                        {
                            switch (b)
                            {
                                case 0xF7:
                                    // write 2 bytes CRC
                                    crc.Split(out crcLow, out crcHigh);
                                    b = crcHigh;
                                    opStatus = OpStatus.WriteCRCLow;
                                    doDrq = false;
                                    allowCrcReset = false;
                                    break;
                                case 0xF8:
                                case 0xF9:
                                case 0xFA:
                                case 0xFB:
                                case 0xFD:
                                case 0xFE:
                                    crc = 0xFFFF;
                                    break;
                            }
                        }
                    }
                    WriteByte(b, allowCrcReset);
                    if (IndexesFound > 1)
                    {
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                        delayBytes = 0;
                    }
                    else
                    {
                        if (doDrq)
                            SetDRQ();
                    }
                    break;
                case OpStatus.WriteCRCLow:
                    opStatus = OpStatus.WritingData;
                    WriteByte(crcLow, false);
                    SetDRQ();
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (Busy)
                SetCommandPulseReq(delayBytes, delayTime, WriteTrackCallback);
        }
        private void ReadTrackCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 0;

            byte b = ReadByte(true);

            switch (opStatus)
            {
                case OpStatus.Prepare:
                    ResetIndexCount();

                    if (delay)
                        opStatus = OpStatus.Delay;
                    else
                        opStatus = OpStatus.SeekingIndexHole;

                    break;

                case OpStatus.Delay:
                    delayTime = STANDARD_DELAY_TIME_IN_USEC;
                    opStatus = OpStatus.SeekingIndexHole;
                    break;
                case OpStatus.SeekingIndexHole:
                    if (IndexesFound > 0)
                        opStatus = OpStatus.ReadingData;
                    delayBytes = 1;
                    break;
                case OpStatus.ReadingData:
                    DataRegister = b;
                    if (Drq)
                    {
                        Log.LogDebug($"Error: Lost Data. Command: {command} OpStatus: {opStatus}");
                        LostData = true;
                    }
                    SetDRQ();
                    if (IndexesFound > 1)
                    {
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        delayBytes = 1;
                    }
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (Busy)
                SetCommandPulseReq(delayBytes, delayTime, ReadTrackCallback);
        }
        private void MotorOnCallback()
        {
            MotorOn = true;
            sound.DriveMotorRunning = true;
            clock.ActivatePulseReq(motorOffPulseReq);
        }
        private void MotorOffCallback()
        {
            Log.LogDebug("FDC Motor Off");

            MotorOn = false;
            sound.DriveMotorRunning = false;
            IntMgr.FdcMotorOffNmiLatch.Latch();
        }

        private void SeekAddressData(byte ByteRead, OpStatus NextStatus, bool Check)
        {
            damBytesChecked++;
            switch (opStatus)
            {
                case OpStatus.SeekingIDAM:
                    if (IndexesFound >= 5)
                    {
                        Log.LogDebug($"Error: Seek Error / Record Not Found. Command: {command} OpStatus: {opStatus} Track Register: {TrackRegister:X2} Sector Register: {SectorRegister:X2} Side Sel Verify: {sideSelectVerify} Indexes Found: {IndexesFound}");
                        SeekError = true;
                        opStatus = OpStatus.NMI;
                    }
                    else
                    {
                        switch (ByteRead)
                        {
                            case Floppy.IDAM:
                                if (track?.HasIdamAt(TrackDataIndex, DoubleDensitySelected) ?? false)
                                {
                                    Log.LogDebug($"IDAM Found. Command: {command} OpStatus: {opStatus} Trk Register: {TrackRegister:X2} Physical Trk: {PhysicalTrackNum:X2} Sec Register: {SectorRegister:X2} Side Sel Verify: {sideSelectVerify} Indexes Found: {IndexesFound}");
                                    readAddressIndex = 0;
                                    ResetCRC();
                                    crc = Lib.Crc(crc, ByteRead);
                                    opStatus = OpStatus.ReadingAddressData;
                                }
                                idamBytesFound = 0;
                                break;
                            default:
                                idamBytesFound = 0;
                                break;
                        }
                    }
                    break;
                case OpStatus.ReadingAddressData:
                    Log.LogDebug($"Reading address data byte {readAddressIndex}: {ByteRead:X2}");
                    readAddressData[readAddressIndex] = ByteRead;
                    if (readAddressIndex == ADDRESS_DATA_CRC_HIGH_BYTE - 1)
                    {
                        // save the value before the first crc on the sector comes in
                        crcCalc = crc;
                    }
                    else if (readAddressIndex >= ADDRESS_DATA_CRC_LOW_BYTE)
                    {
                        damBytesChecked = 0;

                        crcHigh = readAddressData[ADDRESS_DATA_CRC_HIGH_BYTE];
                        crcLow = readAddressData[ADDRESS_DATA_CRC_LOW_BYTE];
                        CrcError = crcCalc != Lib.CombineBytes(crcLow, crcHigh);

                        var match = (TrackRegister == readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE] &&
                                     (!sideSelectVerify || (sideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] != 0))) &&
                                     (SectorRegister == readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE]));

                        if (readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE] != PhysicalTrackNum)
                            Log.LogDebug($"Track Mismatch! Read {readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]:X2} on physical track {PhysicalTrackNum:X2} (CRC Error: {CrcError})");

                        if (Check && !match)
                        {
                            opStatus = OpStatus.SeekingIDAM;
                            Log.LogDebug(string.Format("Address data not matching, continuing read. {0}{1}{2} Physical Track: {3:X2}",
                                (TrackRegister != readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]) ? "Track Register: " + TrackRegister.ToHexString() + " Track Read: " + readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE].ToHexString() : "",
                                (sideSelectVerify && (sideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 0))) ? "Side expected: " + (sideOneExpected ? "1" : "0") + " Side Read: " + (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 1 ? "1" : "0") : "",
                                (SectorRegister != readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE]) ? " Sector Register: " + SectorRegister.ToHexString() + " Sector Read: " + readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE].ToHexString() : "",
                                PhysicalTrackNum));
                        }
                        else
                        {
                            if (match)
                                Log.LogDebug(string.Format("Correct Address Found. Command: {0} OpStatus: {1} " +
                                                                "Track Register: {2:X2} Sector Register: {3:X2} Side Sel Verify: {4} Indexes Found: {5}",
                                                                command, opStatus, TrackRegister, SectorRegister, sideSelectVerify, IndexesFound));
                            else
                                Log.LogDebug(string.Format("No Match: Track found {0:X2} vs {1:X2} (Physical {2:X2}); Side found {3} vs {4}, Sector found {5:X2} vs {6:X2}",
                                    readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE],
                                    TrackRegister,
                                    PhysicalTrackNum,
                                    readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE],
                                    sideOneExpected ? 1 : 0,
                                    readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE],
                                    SectorRegister));

                            sectorLength = Floppy.GetDataLengthFromCode(readAddressData[ADDRESS_DATA_SECTOR_SIZE_BYTE]);
                            
                            if (CrcError)
                            {
                                Log.LogDebug(string.Format("Address CRC Error - Drv: {0} Trk: {1:X2} Side: {2:X2} Sec: {3:X2}",
                                    CurrentDriveNumber, TrackRegister, sideOneExpected ? 1 : 0, SectorRegister));
                                opStatus = OpStatus.SeekingIDAM;
                            }
                            else
                            {
                                opStatus = NextStatus;
                            }
                        }
                    }
                    readAddressIndex++;
                    break;
                default:
                    throw new Exception();
            }
        }

        private void StepUp() => SetTrackNumber(CurrentDrive.PhysicalTrackNumber + 1);
        private void StepDown() => SetTrackNumber(CurrentDrive.PhysicalTrackNumber - 1);
        
        private void UpdateTrack()
        {
            track = CurrentDrive.Floppy?.GetTrack(CurrentDrive.PhysicalTrackNumber, SideOneSelected);
        }
        private void SetTrackNumber(int TrackNum)
        {
            byte trackNum = (byte)(Math.Max(0, Math.Min(MAX_TRACKS, TrackNum)));

            if (CurrentDrive.PhysicalTrackNumber != trackNum)
            {
                CurrentDrive.PhysicalTrackNumber = trackNum;
                UpdateTrack();
                sound.TrackStep();
                if (CurrentDrive.OnTrackZero)
                    TrackRegister = 0;

                Log.LogDebug($"Drive {CurrentDriveNumber} Physical Track Step to {PhysicalTrackNum:X2}");
            }
        }
        internal static bool IsDAM(byte b, out bool SectorDeleted)
        {
            switch (b)
            {
                case Floppy.DAM_DELETED:
                    SectorDeleted = true;
                    return true;
                case Floppy.DAM_NORMAL:
                case 0xF9:
                case 0xFA:
                    SectorDeleted = false;
                    return true;
                default:
                    SectorDeleted = false;
                    return false;
            }
        }
        private void SetCommandPulseReq(int Bytes, ulong DelayInUsec, Clock.ClockCallback Callback)
        {
            commandPulseReq.Expire();
            nextCallback = null;
            isPolling = false;

            if (Bytes == 0 && DelayInUsec == 0)
            {
                Callback();
            }
            else if (Bytes > 0 && DelayInUsec > 0)
            {
                throw new Exception("Can't have both Byte and Time based delays");
            }
            else if (DelayInUsec == 0 && track != null)
            {
                // Byte based delay

                if (!DoubleDensitySelected)
                    Bytes *= 2;

                // we want to come back exactly when the target byte is under the drive head
                targetDataIndex = (TrackDataIndex + Bytes) % track.DataLength;

                if (!DoubleDensitySelected)
                    targetDataIndex &= 0xFFFE;

                nextCallback = Callback;
                isPolling = true;
                commandPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds,
                                               DoubleDensitySelected ? BYTE_POLL_TIME_DD : BYTE_POLL_TIME_SD,
                                               Poll,
                                               false);
                computer.Activate(commandPulseReq);
            }
            else
            {
                // Time based delay
                Log.LogDebug($"Callback Request. Command: {command} Opstatus: {opStatus}");
                commandPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, DelayInUsec, Callback, false);
                computer.Activate(commandPulseReq);
            }
        }
        private void Poll()
        {
            if (isPolling && TrackDataIndex == targetDataIndex)
            {
                // this is the byte we're looking for
                isPolling = false;
                nextCallback();
            }
            else if (isPolling)
            {
                System.Diagnostics.Debug.Assert(TrackDataIndex != targetDataIndex + 1, "Just missed the target!!");
                // keep looking for the target index
                computer.Activate(commandPulseReq);
            }
            else
            {
                throw new Exception("Polling error.");
            }
        }

        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(TrackRegister);
            Writer.Write(SectorRegister);
            Writer.Write(CommandRegister);
            Writer.Write(DataRegister);
            Writer.Write(statusRegister);

            Writer.Write((int)command);
            Writer.Write((int)opStatus);

            Writer.Write(DoubleDensitySelected);
            Writer.Write(sectorDeleted);
            Writer.Write(Busy);
            Writer.Write(Drq);
            Writer.Write(SeekError);
            Writer.Write(CrcError);
            Writer.Write(LostData);
            Writer.Write(lastStepDirUp);
            Writer.Write(writeProtected);
            Writer.Write(MotorOn);
            Writer.Write(stepRateInUsec);
            
            Writer.Write(verify);
            Writer.Write(updateRegisters);
            Writer.Write(sideSelectVerify);
            Writer.Write(sideOneExpected);
            Writer.Write(markSectorDeleted);
            Writer.Write(multipleRecords);

            Writer.Write(readAddressData);
            Writer.Write(readAddressIndex);
            Writer.Write(idamBytesFound);
            Writer.Write(damBytesChecked);
            Writer.Write(sectorLength);
            Writer.Write(bytesRead);
            Writer.Write(bytesToWrite);
            Writer.Write(indexCheckStartTick);
            Writer.Write(isPolling);
            Writer.Write(targetDataIndex);

            Writer.Write(crc);
            Writer.Write(crcCalc);
            Writer.Write(crcHigh);
            Writer.Write(crcLow);

            for (byte b = 0; b < NUM_DRIVES; b++)
                drives[b].Serialize(Writer);

            Writer.Write(currentDriveNumber);
            Writer.Write(SideOneSelected);

            commandPulseReq.Serialize(Writer);
            motorOnPulseReq.Serialize(Writer);
            motorOffPulseReq.Serialize(Writer);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int SerializationVersion)
        {
            try
            {
                bool ok = true;

                TrackRegister =   Reader.ReadByte();
                SectorRegister =  Reader.ReadByte();
                CommandRegister = Reader.ReadByte();
                DataRegister =    Reader.ReadByte();
                statusRegister =  Reader.ReadByte();

                command =  (Command) Reader.ReadInt32();
                opStatus = (OpStatus)Reader.ReadInt32();

                DoubleDensitySelected = Reader.ReadBoolean();
                sectorDeleted =         Reader.ReadBoolean();
                Busy =                  Reader.ReadBoolean();
                Drq =                   Reader.ReadBoolean();
                SeekError =             Reader.ReadBoolean();
                CrcError =              Reader.ReadBoolean();
                LostData =              Reader.ReadBoolean();
                lastStepDirUp =         Reader.ReadBoolean();
                writeProtected =        Reader.ReadBoolean();
                MotorOn =               Reader.ReadBoolean();
                stepRateInUsec =        Reader.ReadUInt64();
                verify =                Reader.ReadBoolean();
                updateRegisters =       Reader.ReadBoolean();
                sideSelectVerify =      Reader.ReadBoolean();
                sideOneExpected =       Reader.ReadBoolean();
                markSectorDeleted =     Reader.ReadBoolean();
                multipleRecords =       Reader.ReadBoolean();

                Array.Copy(Reader.ReadBytes(ADDRESS_DATA_BYTES), readAddressData, ADDRESS_DATA_BYTES);

                readAddressIndex = Reader.ReadByte();
                idamBytesFound =   Reader.ReadInt32();
                damBytesChecked =  Reader.ReadInt32();
                sectorLength =     Reader.ReadInt32();
                bytesRead =        Reader.ReadInt32();
                bytesToWrite =     Reader.ReadInt32();

                indexCheckStartTick = Reader.ReadUInt64();
                isPolling =           Reader.ReadBoolean();
                targetDataIndex =     Reader.ReadInt32();

                crc =     Reader.ReadUInt16();
                crcCalc = Reader.ReadUInt16();
                crcHigh = Reader.ReadByte();
                crcLow =  Reader.ReadByte();

                for (byte b = 0; b < NUM_DRIVES; b++)
                {
                    ok &= drives[b].Deserialize(Reader, SerializationVersion);
                    if (drives[b].IsLoaded)
                        if (System.IO.File.Exists(drives[b].Floppy.FilePath))
                            Storage.SaveDefaultDriveFileName(b, drives[b].Floppy.FilePath);
                }

                currentDriveNumber = Reader.ReadByte();
                SideOneSelected = Reader.ReadBoolean();

                Clock.ClockCallback callback;

                switch (command)
                {
                    case Command.ReadAddress:
                        callback = ReadAddressCallback;
                        break;
                    case Command.ReadSector:
                        callback = ReadSectorCallback;
                        break;
                    case Command.ReadTrack:
                        callback = ReadTrackCallback;
                        break;
                    case Command.WriteSector:
                        callback = WriteSectorCallback;
                        break;
                    case Command.WriteTrack:
                        callback = WriteTrackCallback;
                        break;
                    case Command.Restore:
                    case Command.Seek:
                    case Command.Step:
                        callback = TypeOneCommandCallback;
                        break;
                    default:
                        callback = null;
                        break;
                }

                if (isPolling)
                {
                    nextCallback = callback;
                    callback = Poll;
                }

                ok &= commandPulseReq.Deserialize(Reader, callback, SerializationVersion) &&
                      motorOnPulseReq.Deserialize(Reader, MotorOnCallback, SerializationVersion) &&
                      motorOffPulseReq.Deserialize(Reader, MotorOffCallback, SerializationVersion);

                if (ok)
                {
                    if (commandPulseReq.Active)
                        computer.AddPulseReq(commandPulseReq);
                    if (motorOnPulseReq.Active)
                        computer.AddPulseReq(motorOnPulseReq);
                    if (motorOffPulseReq.Active)
                        computer.AddPulseReq(motorOffPulseReq);

                    UpdateTrack();
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private byte ReadByte(bool AllowResetCRC)
        {
            byte b = ReadTrackByte();

            if (AllowResetCRC)
                crc = UpdateCRC(crc, b, true, DoubleDensitySelected);
            else
                crc = Lib.Crc(crc, b);

            return b;
        }
        private void WriteByte(byte B, bool AllowResetCRC)
        {
            if (AllowResetCRC)
                crc = UpdateCRC(crc, B, true, DoubleDensitySelected);
            else
                crc = Lib.Crc(crc, B);

            WriteTrackByte(B);
        }
        private void ResetCRC() => crc = DoubleDensitySelected ? Floppy.CRC_RESET_A1_A1_A1 : Floppy.CRC_RESET;

        // REGISTER I/O

        public void FdcIoEvent(byte portNum, byte value, bool isOut)
        {
            if (isOut)
            {
                switch (portNum)
                {
                    case 0xE4:
                    case 0xE5:
                    case 0xE6:
                    case 0xE7:
                        IntMgr.InterruptEnableStatus = value;
                        break;
                    case 0xF0:
                        SetCommandRegister(value);
                        break;
                    case 0xF1:
                        SetTrackRegister(value);
                        break;
                    case 0xF2:
                        SetSectorRegister(value);
                        break;
                    case 0xF3:
                        SetDataRegister(value);
                        break;
                    case 0xF4:
                        if (Enabled)
                            FdcDiskSelect(value);
                        break;
                    default:
                        break;
                }
            }
            else // isIn
            {
                switch (portNum)
                {
                    case 0xF0:
                        GetStatusRegister();
                        break;
                    case 0xF1:
                        GetTrackRegister();
                        break;
                    case 0xF2:
                        GetSectorRegister();
                        break;
                    case 0xF3:
                        GetDataRegister();
                        break;
                    default:
                        break;
                }
            }
        }

        private void GetStatusRegister()
        {
            UpdateStatus();

            Log.LogDebug($"Get status register: {statusRegister:X2}");

            if (Enabled)
                ports.SetPortDirect(0xF0, statusRegister);
            else
                ports.SetPortDirect(0xF0, 0xFF);

            IntMgr.FdcNmiLatch.Unlatch();
            IntMgr.FdcMotorOffNmiLatch.Unlatch();
        }
        private void GetTrackRegister()
        {
            ports.SetPortDirect(0xF1, Enabled ? TrackRegister : (byte)0xFF);
            Log.LogDebug($"Get track register: {TrackRegister:X2}");
        }
        private void GetSectorRegister()
        {
            ports.SetPortDirect(0xF2, Enabled ? SectorRegister : (byte)0xFF);
            Log.LogDebug($"Get sector register: {SectorRegister:X2}");
        }
        private void GetDataRegister()
        {
            Log.LogDebug($"Read data register: {DataRegister:X2}");
            ports.SetPortDirect(0xF3, Enabled ? DataRegister : (byte)0xFF);
            Drq = false;
        }
        private void SetCommandRegister(byte value)
        {
            command = GetCommand(value);

            Log.LogDebug($"Setting command register: FDC Command {command} - Drv {CurrentDriveNumber} [{value:X2}]");

            try
            {
                CommandRegister = value;

                IntMgr.FdcNmiLatch.Unlatch();
                IntMgr.FdcMotorOffNmiLatch.Unlatch();

                if (!Busy || (command != Command.Reset))
                {
                    Drq = false;
                    LostData = false;
                    SeekError = false;
                    CrcError = false;
                    writeProtected = CurrentDrive.WriteProtected;
                    sectorDeleted = false;
                    Busy = true;
                }
                /*
                if (CurrentDrive.IsUnloaded && command != Command.Reset)
                {
                    switch (command)
                    {
                        case Command.Seek:
                        default:
                            SeekError = true;
                            DoNmi();
                            return;
                    }
                }
                */
                opStatus = OpStatus.Prepare;
                idamBytesFound = 0;

                switch (command)
                {
                    case Command.Restore:
                        TrackRegister = 0xFF;
                        DataRegister = 0x00;
                        TypeOneCommandCallback();
                        break;
                    case Command.Seek:
                        TypeOneCommandCallback();
                        break;
                    case Command.Step:
                        if (value.IsBitSet(6))
                            if (value.IsBitSet(5))
                                lastStepDirUp = false;
                            else
                                lastStepDirUp = true;
                        TypeOneCommandCallback();
                        break;
                    case Command.ReadSector:
                        delay = CommandRegister.IsBitSet(2);
                        ReadSectorCallback();
                        break;
                    case Command.WriteSector:
                        WriteSectorCallback();
                        break;
                    case Command.ReadAddress:
                        ReadAddressCallback();
                        break;
                    case Command.Reset:
                        // puts FDC in mode 1
                        commandPulseReq.Expire();
                        Busy = false;
                        opStatus = OpStatus.OpDone;
                        break;
                    case Command.ForceInterruptImmediate:
                        DoNmi();
                        opStatus = OpStatus.OpDone;
                        break;
                    case Command.ForceInterrupt:
                        Log.LogDebug($"Unimplemented FDC Command: {command} [{CommandRegister:X2}]");
                        DoNmi();
                        opStatus = OpStatus.OpDone;
                        break;
                    case Command.ReadTrack:
                        delay = CommandRegister.IsBitSet(2);
                        ReadTrackCallback();
                        break;
                    case Command.WriteTrack:
                        delay = CommandRegister.IsBitSet(2);
                        WriteTrackCallback();
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"Error setting command register: FDC Command: Drv {CurrentDriveNumber} - {command} [{value:X2}]");
            }
        }
        private void SetTrackRegister(byte value)
        {
            TrackRegister = value;
            Log.LogDebug($"Set track register: {TrackRegister:X2}");
        }
        private void SetSectorRegister(byte value)
        {
            SectorRegister = value;
            Log.LogDebug($"Set sector register: {SectorRegister:X2}");
        }
        private void SetDataRegister(byte value)
        {
            DataRegister = value;
            Drq = false;
            Log.LogDebug($"Set data register: {DataRegister:X2}");
        }
        private void FdcDiskSelect(byte Value)
        {
            Log.LogDebug($"Disk select: {Value:X2}");

            byte? floppyNum = null;

            if (Value.IsBitSet(0))
                floppyNum = 0;
            else if (Value.IsBitSet(1))
                floppyNum = 1;
            else if (Value.IsBitSet(2))
                floppyNum = 2;
            else if (Value.IsBitSet(3))
                floppyNum = 3;

            if (floppyNum.HasValue && CurrentDriveNumber != floppyNum.Value)
                CurrentDriveNumber = floppyNum.Value;

            bool sideOne = Value.IsBitSet(4);
            if (SideOneSelected != sideOne)
            {
                SideOneSelected = sideOne;
                Log.LogDebug(string.Format("FDC Side select: {0}", sideOne ? 1 : 0));
            }

            if (Value.IsBitSet(6))
                clock.Wait();

            DoubleDensitySelected = Value.IsBitSet(7);

            if (MotorOn)
                MotorOnCallback();
            else
                computer.Activate(motorOnPulseReq);
        }
        private static Command GetCommand(byte CommandRegister)
        {
            switch (CommandRegister & 0xF0)
            {
                case 0x00:
                    return Command.Restore;
                case 0x10:
                    return Command.Seek;
                case 0x20:
                case 0x30:
                case 0x40:
                case 0x50:
                case 0x60:
                case 0x70:
                    return Command.Step;
                case 0x80:
                case 0x90:
                    return Command.ReadSector;
                case 0xA0: // write sector  
                case 0xB0:
                    return Command.WriteSector;
                case 0xC0: // read address
                    return Command.ReadAddress;
                case 0xD0:
                    if (CommandRegister == 0xD0)
                        return Command.Reset;
                    else if (CommandRegister == 0xD8)
                        return Command.ForceInterruptImmediate;
                    else
                        return Command.ForceInterrupt;
                case 0xE0: // read track
                    return Command.ReadTrack;
                case 0xF0:  // write track
                    return Command.WriteTrack;
                default:
                    return Command.Invalid;
            }
        }
        private void UpdateStatus()
        {
            //    Type I Command Status values are:
            //    ------------------
            //    80H - Not ready
            //    40H - Write protect
            //    20H - Head loaded
            //    10H - Seek error
            //    08H - CRC error
            //    04H - Track 0
            //    02H - Index Hole
            //    01H - Busy

            //    Type II / III Command Status values are:
            //    80H - Not ready
            //    40H - NA (Read) / Write Protect (Write)
            //    20H - Deleted (Read) / Write Fault (Write)
            //    10H - Record Not Found
            //    08H - CRC error
            //    04H - Lost Data
            //    02H - DRQ
            //    01H - Busy

            //    60H - Rec Type / F8 (1771)
            //    40H - F9 (1771)
            //    20H - F8 (1791) / FA (1771)
            //    00H - FB (1791, 1771)

            statusRegister = 0x00;
            bool indexHole = false;
            bool headLoaded = false;
            if (!MotorOn)
            {
                statusRegister |= 0x80; // not ready
            }
            else if (DriveIsUnloaded(CurrentDriveNumber))
            {
               // indexHole = true;
            }
            else
            {
                indexHole = IndexDetect;
                headLoaded = true;
            }

            switch (command)
            {
                case Command.Restore:
                case Command.Seek:
                case Command.Step:
                case Command.Reset:
                    if (writeProtected)
                        statusRegister |= 0x40;   // Bit 6: Write Protect detect
                    if (headLoaded)
                        statusRegister |= 0x20;   // Bit 5: head loaded and engaged
                    if (SeekError)
                        statusRegister |= 0x10;   // Bit 4: Seek error
                    if (CrcError)
                        statusRegister |= 0x08;   // Bit 3: CRC Error
                    if (CurrentDrive.OnTrackZero) // Bit 2: Track Zero detect
                    {
                        statusRegister |= 0x04;
                        Log.LogDebug("Track Zero Detected Drive " + PhysicalTrackNum.ToString());
                    }
                    if (indexHole)
                        statusRegister |= 0x02;   // Bit 1: Index Detect
                    break;
                case Command.ReadAddress:
                    if (SeekError)
                        statusRegister |= 0x10; // Bit 4: Record Not found
                    if (CrcError)
                        statusRegister |= 0x08; // Bit 3: CRC Error
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (Drq)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.ReadSector:
                    if (sectorDeleted)
                        statusRegister |= 0x20; // Bit 5: Detect "deleted" address mark
                    if (SeekError)
                        statusRegister |= 0x10; // Bit 4: Record Not found
                    if (CrcError)
                        statusRegister |= 0x08; // Bit 3: CRC Error
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (Drq)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.ReadTrack:
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (Drq)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.WriteSector:
                    if (writeProtected)
                        statusRegister |= 0x40; // Bit 6: Write Protect detect
                    if (SeekError)
                        statusRegister |= 0x10; // Bit 4: Record Not found
                    if (CrcError)
                        statusRegister |= 0x08; // Bit 3: CRC Error
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (Drq)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.WriteTrack:
                    if (writeProtected)
                        statusRegister |= 0x40; // Bit 6: Write Protect detect
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (Drq)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
            }
            if (Busy)
                statusRegister |= 0x01; // Bit 0: Busy
        }

        // INTERRUPTS

        private void DoNmi()
        {
            Log.LogDebug($"NMI requested. Command: {command} Opstatus: {opStatus}");
            Drq = false;
            Busy = false;
            IntMgr.FdcNmiLatch.Latch();
        }
        private void SetDRQ()
        {
            Drq = true;
            Log.LogDebug("DRQ Set, Disk angle: " + DiskAngleDegrees);
        }

        // SPIN SIMULATION

        private ulong DiskAngle
        {
            get => DISK_ANGLE_DIVISIONS * (clock.TickCount % TicksPerDiskRev) / TicksPerDiskRev;
        }
        public bool IndexDetect
        {
            get => MotorOn && DiskAngle < INDEX_PULSE_END;
        }
        public int IndexesFound
        {
            get => (int)((clock.TickCount - indexCheckStartTick) / TicksPerDiskRev);
        }
        public void ResetIndexCount()
        {
            // make as if we started checking just after the index pulse started
            indexCheckStartTick = clock.TickCount - (clock.TickCount % TicksPerDiskRev) + 10;
        }

        // TRACK DATA HANLDING

        public int TrackDataIndex
        {
            get =>
                (int)(DiskAngle * (ulong)(track?.DataLength ?? 
                    (DoubleDensitySelected ? Floppy.STANDARD_TRACK_LENGTH_DOUBLE_DENSITY 
                                           : Floppy.STANDARD_TRACK_LENGTH_SINGLE_DENSITY)) / DISK_ANGLE_DIVISIONS);
            
        }
        private byte ReadTrackByte() => track?.ReadByte(TrackDataIndex, DoubleDensitySelected) ?? 0;
        
        private void WriteTrackByte(byte B) => track?.WriteByte(TrackDataIndex, DoubleDensitySelected, B);
        
        private int CommandType(Command Command)
        {
            switch (Command)
            {
                case Command.Restore:
                case Command.Seek:
                case Command.Step:
                    return 1;
                case Command.ReadSector:
                case Command.WriteSector:
                    return 2;
                case Command.ReadTrack:
                case Command.WriteTrack:
                case Command.ReadAddress:
                    return 3;
                case Command.ForceInterrupt:
                case Command.ForceInterruptImmediate:
                    return 4;
                default:
                    return 0;
            }
        }
        public void Dispose()
        {
            motorOffPulseReq?.Expire();
            motorOnPulseReq?.Expire();
            commandPulseReq?.Expire();
        }
    }
}