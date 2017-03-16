/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Linq;
using System.IO;

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
        private const ulong INDEX_PULSE_WIDTH_IN_DIVISIONS = 10000;//INDEX_PULSE_WIDTH_IN_USEC * DISK_ANGLE_DIVISIONS / TRACK_FULL_ROTATION_TIME_IN_USEC;
        private const ulong INDEX_PULSE_START = 0;//10000;
        private const ulong INDEX_PULSE_END = 10000;//20000;
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
        public bool DrqStatus { get; private set; }
        public bool CrcError { get; private set; }
        public bool LostData { get; private set; }
        public bool SeekError { get; private set; }
        private bool drq;
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
        private DriveState CurrentDrive { get { return (CurrentDriveNumber >= NUM_DRIVES) ? null : drives[CurrentDriveNumber]; } }
        
        private bool SideOne { get; set; }

        // Operation state
        private Command command;
        private OpStatus opStatus = OpStatus.OpDone;
        private byte[] readAddressData = new byte[ADDRESS_DATA_BYTES];
        private byte readAddressIndex;
        private int damBytesChecked;
        private int idamBytesFound;
        private int sectorLength;
        private int bytesRead;
        private int indexesFound;
        private bool indexFoundLastCheck;
        private int bytesToWrite;
        private ushort crc;
        private ushort crcCalc;
        private byte crcHigh, crcLow;
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

            TicksPerDiskRev = clock.TicksPerSec / 5;

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

        public string OperationStatus {  get { return opStatus.ToString(); } }
        public string CommandStatus { get { return command.ToString(); } }
        public byte PhysicalTrackNum { get { return CurrentDrive.PhysicalTrackNumber; } }
        public string DiskAngleDegrees { get { return ((double)DiskAngle / DISK_ANGLE_DIVISIONS * 360).ToString("000.000") + " degrees"; } }
        public byte ValueAtTrackDataIndex { get { return track?.ReadByte(TrackDataIndex, null) ?? 0; } }
        public Floppy GetFloppy(int DriveNumber)
        {
            return drives[DriveNumber].Floppy;
        }

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
            drq = DrqStatus = false;
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
        }
        public void LoadFloppy(byte DriveNum, string FilePath)
        {
            if (FilePath.Length == 0)
            {
                UnloadDrive(DriveNum);
            }
            else if (!File.Exists(FilePath))
            {
                Log.LogDebug(string.Format("File {0} does not exist. Load cancelled.", FilePath));
            }
            else
            {
                Floppy f = null;
                try
                {
                    f = Floppy.LoadDisk(FilePath);
                }
                catch (Exception ex)
                {
                    ex.Data["ExtraMessage"] = "Failed to load flopy from " + FilePath;
                    Log.LogException(ex);
                }
                if (f == null)
                    UnloadDrive(DriveNum);
                else
                    LoadDrive(DriveNum, f);
            }
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
                    Storage.SaveBinaryFile(f.FilePath, f.Serialize(ForceDMK: false));
            }
            catch (Exception ex)
            {
                ex.Data["ExtraMessage"] = string.Format("Error saving drive number {0}:", DriveNum);
                Log.LogException(ex, ExceptionHandlingOptions.InformUser);
            }
        }
        private void LoadDrive(byte DriveNum, Floppy Floppy)
        {
            drives[DriveNum].Floppy = Floppy;
            UpdateTrack();
        }
        public void UnloadDrive(byte DriveNum)
        {
            LoadDrive(DriveNum, null);
            UpdateTrack();
        }

        public string StatusReport
        {
            get
            {
                return "Dsk: " + CurrentDriveNumber.ToString() +
                       ":S" + (SideOne ? "1" : "0") +
                       ":T" + CurrentDrive.PhysicalTrackNumber.ToString("00") +
                       ":S" + SectorRegister.ToString("00");
            }
        }

        public bool? DriveBusyStatus
        {
            get
            {
                if (Busy)
                    return true;
                else if (MotorOn)
                    return false;
                else
                    return null;
            }
        }
        public bool AnyDriveLoaded
        {
            get { return drives.Any(d => !d.IsUnloaded); }
        }
        public bool Available
        {
            get { return Enabled && !DriveIsUnloaded(0); }
        }
        public void Disable()
        {
            Enabled = false;
        }
        public bool DriveIsUnloaded(byte DriveNum)
        {
            return drives[DriveNum].IsUnloaded;
        }
        public string FloppyFilePath(byte DriveNum)
        {
            return drives[DriveNum].Floppy?.FilePath ?? String.Empty;
        }
        public bool? DiskHasChanged(byte DriveNum)
        {
            if (drives[DriveNum].IsUnloaded)
                return null;
            else
                return drives[DriveNum].Floppy.Changed;
        }
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
        public bool DRQ { get { return drq; } }

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
                        Log.LogDebug(string.Format("Track Register {0} != Physical Track Number {1}", TrackRegister, CurrentDrive.PhysicalTrackNumber));

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
                        Log.LogDebug(string.Format(opStatus.ToString() + " Verify Track failed: Track Register: {0} Track Read {1}", TrackRegister, readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]));
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
                    if (damBytesChecked++ > (DoubleDensitySelected ? 43 : 30))
                    {
                        Log.LogDebug(string.Format("Error: Seek Error / Record Not Found. Command: {0} OpStatus: {1}", command, opStatus));
                        SeekError = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else if (IsDAM(b, out sectorDeleted))
                    {
                        ResetCRC();
                        crc = Lib.Crc(crc, b);
                        opStatus = OpStatus.ReadingData;
                        bytesRead = 0;
                    }
                    delayBytes = 1;
                    break;
                case OpStatus.ReadingData:
                    if (drq)
                    {
                        Log.LogDebug(string.Format("Error: Lost Data. Command: {0} OpStatus: {1} TrackRegister: {2} SectorRegister {3} Bytes Read: {4}",
                                                      command, opStatus, TrackRegister, SectorRegister, bytesRead));
                        LostData = true;
                    }
                    if (++bytesRead >= sectorLength)
                    {
                        crcCalc = crc;
                        opStatus = OpStatus.ReadCRCHigh;
                    }
                    Log.LogDebug(string.Format("FDC Write to data register: {0}", b.ToHexString()));
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
                        Log.LogDebug(string.Format("Error: CRC Error. Command: {0} OpStatus: {1}", command, opStatus));
                        Log.LogDebug(string.Format("Data CRC Error - Drv: {0} Trk: {1} Side: {2} Sec: {3}", CurrentDriveNumber, TrackRegister, sideOneExpected ? 1 : 0, SectorRegister));
                        //opStatus = OpStatus.SeekingIDAM;
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
                        Log.LogDebug(string.Format("Cancelling due to Write Protect: Command: {0} OpStatus: {1}", command, opStatus));

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
                    if (drq)
                    {
                        Log.LogDebug(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
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
                    if (drq)
                    {
                        Log.LogDebug(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
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
                        Log.LogDebug(string.Format("Cancelling due to Write Protect: Command: {0} OpStatus: {1}", command, opStatus));

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
                    if (drq)
                    {
                        Log.LogDebug(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
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
                    if (drq)
                    {
                        Log.LogDebug(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
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
                    if (drq)
                    {
                        Log.LogDebug(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
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
        // TODO: Events for these
        private void MotorOnCallback()
        {
            MotorOn = true;
            sound.DriveMotorRunning = true;
            clock.RegisterPulseReq(motorOffPulseReq);
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
                        Log.LogDebug(string.Format("Error: Seek Error / Record Not Found. Command: {0} OpStatus: {1} Track Register: {2} Sector Register: {3} Side Sel Verify: {4} Indexes Found: {5}", command, opStatus, TrackRegister, SectorRegister, sideSelectVerify, IndexesFound));
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
                                    Log.LogDebug(string.Format("IDAM Found. Command: {0} OpStatus: {1} Track Register: {2} Sector Register: {3} Side Sel Verify: {4} Indexes Found: {5}", command, opStatus, TrackRegister, SectorRegister, sideSelectVerify, IndexesFound));
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

                        if (Check && (TrackRegister != readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE] ||
                                     (sideSelectVerify && (sideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 0))) ||
                                     (SectorRegister != readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE])))
                        {
                            opStatus = OpStatus.SeekingIDAM;
                            Log.LogDebug(string.Format("Address data not matching, continuing read. {0}{1}{2}",
                                (TrackRegister != readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]) ? "Track Register: " + TrackRegister.ToString() + " Track Read: " + readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE].ToString() : "",
                                (sideSelectVerify && (sideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 0))) ? "Side expected: " + (sideOneExpected ? "1" : "0") + " Side Read: " + (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 1 ? "1" : "0") : "",
                                (SectorRegister != readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE]) ? " Sector Register: " + SectorRegister.ToString() + " Sector Read: " + readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE].ToString() : ""));
                        }
                        else
                        {
                            
                            Log.LogDebug(string.Format("Correct Address Found. Command: {0} OpStatus: {1} " +
                                                            "Track Register: {2} Sector Register: {3} Side Sel Verify: {4} Indexes Found: {5}",
                                                            command, opStatus, TrackRegister, SectorRegister, sideSelectVerify, IndexesFound));

                            sectorLength = Floppy.GetDataLengthFromCode(readAddressData[ADDRESS_DATA_SECTOR_SIZE_BYTE]);
                            
                            if (CrcError)
                            {
                                Log.LogDebug(string.Format("Address CRC Error - Drv: {0} Trk: {1} Side: {2} Sec: {3}",
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
        private void StepUp()
        {
            SetTrackNumber(CurrentDrive.PhysicalTrackNumber + 1);
        }
        private void StepDown()
        {
            SetTrackNumber(CurrentDrive.PhysicalTrackNumber - 1);
        }
        private void UpdateTrack()
        {
            byte? trackNum = currentDriveNumber;
            track = CurrentDrive.Floppy?.GetTrack(CurrentDrive.PhysicalTrackNumber, SideOne);
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

                Log.LogDebug(string.Format("Drive {0} Physical Track Step to {1}",
                                                CurrentDriveNumber, CurrentDrive.PhysicalTrackNumber));
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

            if (Bytes == 0 && DelayInUsec == 0)
            {
                Callback();
            }
            else if (Bytes > 0 && DelayInUsec > 0)
            {
                throw new Exception();
            }
            else if (DelayInUsec == 0 && track != null)
            {
                if (Bytes == 0)
                {
                    Callback();
                }
                else
                {
                    if (!DoubleDensitySelected)
                        Bytes *= 2;

                    // we want to come back exactly as the target byte is under the drive head
                    int targetIndex = (TrackDataIndex + Bytes) % track.DataLength;

                    if (!DoubleDensitySelected)
                        targetIndex &= 0x7FFFFFFE;

                    commandPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds,
                                                   DoubleDensitySelected ? BYTE_POLL_TIME_DD : BYTE_POLL_TIME_SD,
                                                   () => { if (TrackDataIndex == targetIndex)
                                                           Callback();
                                                       else
                                                           computer.RegisterPulseReq(commandPulseReq);                                                        
                                                   },
                                                   false);
                    computer.RegisterPulseReq(commandPulseReq);
                }
            }
            else
            {
                int bytesToAdvance = Bytes + (int)(DelayInUsec / BYTE_TIME_IN_USEC_DD);
                ulong delayTime = DelayInUsec + (ulong)Bytes * BYTE_TIME_IN_USEC_DD;

                if (!DoubleDensitySelected)
                {
                    bytesToAdvance *= 2;
                    delayTime *= 2;
                }
                if (delayTime > 0)
                {
                    Log.LogDebug(string.Format("Callback Request. Command: {0} Opstatus: {1}", command, opStatus));
                    commandPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, delayTime, Callback, false);
                    computer.RegisterPulseReq(commandPulseReq);
                }
                else
                {
                    Callback();
                }
            }
        }

        // SNAPSHOTS

        public void Serialize(BinaryWriter Writer)
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
            Writer.Write(drq);
            Writer.Write(DrqStatus);
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

            // remove at next schema change
            Writer.Write(TrackDataIndex);
            
            Writer.Write(readAddressData);
            Writer.Write(readAddressIndex);
            Writer.Write(idamBytesFound);
            Writer.Write(damBytesChecked);
            Writer.Write(sectorLength);
            Writer.Write(bytesRead);
            Writer.Write(bytesToWrite);
            Writer.Write(indexesFound);
            Writer.Write(indexFoundLastCheck);

            Writer.Write(crc);
            Writer.Write(crcCalc);
            Writer.Write(crcHigh);
            Writer.Write(crcLow);

            for (byte b = 0; b < NUM_DRIVES; b++)
                drives[b].Serialize(Writer);

            Writer.Write(currentDriveNumber);
            Writer.Write(SideOne);

            commandPulseReq.Serialize(Writer);
            motorOnPulseReq.Serialize(Writer);
            motorOffPulseReq.Serialize(Writer);
        }
        public void Deserialize(BinaryReader Reader)
        {
            TrackRegister = Reader.ReadByte();
            SectorRegister = Reader.ReadByte();
            CommandRegister = Reader.ReadByte();
            DataRegister = Reader.ReadByte();
            statusRegister = Reader.ReadByte();

            command = (Command)Reader.ReadInt32();
            opStatus = (OpStatus)Reader.ReadInt32();

            DoubleDensitySelected = Reader.ReadBoolean();
            sectorDeleted = Reader.ReadBoolean();
            Busy = Reader.ReadBoolean();
            drq = Reader.ReadBoolean();
            DrqStatus = Reader.ReadBoolean();
            SeekError = Reader.ReadBoolean();
            CrcError = Reader.ReadBoolean();
            LostData = Reader.ReadBoolean();
            lastStepDirUp = Reader.ReadBoolean();
            writeProtected = Reader.ReadBoolean();
            MotorOn = Reader.ReadBoolean();
            stepRateInUsec = Reader.ReadUInt64();
            verify = Reader.ReadBoolean();
            updateRegisters = Reader.ReadBoolean();
            sideSelectVerify = Reader.ReadBoolean();
            sideOneExpected = Reader.ReadBoolean();
            markSectorDeleted = Reader.ReadBoolean();
            multipleRecords = Reader.ReadBoolean();

            // Remove at next schema change
            Reader.ReadInt32();

            Array.Copy(Reader.ReadBytes(ADDRESS_DATA_BYTES), readAddressData, ADDRESS_DATA_BYTES);
            readAddressIndex = Reader.ReadByte();
            idamBytesFound = Reader.ReadInt32();
            damBytesChecked = Reader.ReadInt32();
            sectorLength = Reader.ReadInt32();
            bytesRead = Reader.ReadInt32();
            bytesToWrite = Reader.ReadInt32();

            indexesFound = Reader.ReadInt32();
            indexFoundLastCheck = Reader.ReadBoolean();

            crc = Reader.ReadUInt16();
            crcCalc = Reader.ReadUInt16();
            crcHigh = Reader.ReadByte();
            crcLow = Reader.ReadByte();

            for (byte b = 0; b < NUM_DRIVES; b++)
            {
                drives[b].Deserialize(Reader);
                if (drives[b].IsLoaded)
                    if (File.Exists(drives[b].Floppy.FilePath))
                        Storage.SaveDefaultDriveFileName(b, drives[b].Floppy.FilePath);
            }

            currentDriveNumber = Reader.ReadByte();
            SideOne = Reader.ReadBoolean();

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

            commandPulseReq.Deserialize(Reader, callback);
            if (commandPulseReq.Active)
                computer.AddPulseReq(commandPulseReq);

            motorOnPulseReq.Deserialize(Reader, MotorOnCallback);
            if (motorOnPulseReq.Active)
                computer.AddPulseReq(motorOnPulseReq);

            motorOffPulseReq.Deserialize(Reader, MotorOffCallback);
            if (motorOffPulseReq.Active)
                computer.AddPulseReq(motorOffPulseReq);

            UpdateTrack();
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
        private void ResetCRC()
        {
            crc = DoubleDensitySelected ? Floppy.CRC_RESET_A1_A1_A1 : Floppy.CRC_RESET;
        }

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

            Log.LogDebug(string.Format("Get status register: {0}", statusRegister.ToHexString()));

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
            Log.LogDebug(string.Format("Get track register: {0}", TrackRegister.ToHexString()));
        }
        private void GetSectorRegister()
        {
            ports.SetPortDirect(0xF2, Enabled ? SectorRegister : (byte)0xFF);
            Log.LogDebug(string.Format("Get sector register: {0}", SectorRegister.ToHexString()));
        }
        private void GetDataRegister()
        {
            Log.LogDebug(string.Format("Read data register: {0}", DataRegister.ToHexString()));
            ports.SetPortDirect(0xF3, Enabled ? DataRegister : (byte)0xFF);
            drq = DrqStatus = false;
        }
        private void SetCommandRegister(byte value)
        {
            command = GetCommand(value);

            Log.LogDebug(string.Format("Setting command register: FDC Command {0} - Drv {1} [{2}]",
                                            command, CurrentDriveNumber, value.ToHexString()));

            try
            {
                CommandRegister = value;

                IntMgr.FdcNmiLatch.Unlatch();
                IntMgr.FdcMotorOffNmiLatch.Unlatch();

                if (!Busy || (command != Command.Reset))
                {
                    drq = DrqStatus = false;
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
                        Log.LogDebug(string.Format("Unimplemented FDC Command: {0} [{1}]",
                                                        command.ToString(),
                                                        CommandRegister.ToHexString()));
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
                ex.Data["ExtraMessage"] = string.Format("Error setting command register: FDC Command: Drv {0} - {1} [{2}]", CurrentDriveNumber, command, value.ToHexString());
                Log.LogException(ex, ExceptionHandlingOptions.InformUser);
            }
        }
        private void SetTrackRegister(byte value)
        {
            TrackRegister = value;
            Log.LogDebug(string.Format("Set track register: {0}", TrackRegister.ToHexString()));
        }
        private void SetSectorRegister(byte value)
        {
            SectorRegister = value;
            Log.LogDebug(string.Format("Set sector register: {0}", SectorRegister.ToHexString()));
        }
        private void SetDataRegister(byte value)
        {
            DataRegister = value;
            drq = DrqStatus = false;
            Log.LogDebug(string.Format("Set data register: {0}", DataRegister.ToHexString()));
        }
        private void FdcDiskSelect(byte value)
        {
            byte? floppyNum = null;

            if (value.IsBitSet(0))
                floppyNum = 0;
            else if (value.IsBitSet(1))
                floppyNum = 1;
            else if (value.IsBitSet(2))
                floppyNum = 2;
            else if (value.IsBitSet(3))
                floppyNum = 3;

            if (floppyNum.HasValue && CurrentDriveNumber != floppyNum.Value)
            {
                Log.LogDebug(string.Format("FDC Disk select: {0}", floppyNum.Value.ToHexString()));
                CurrentDriveNumber = floppyNum.Value;
            }

            bool sideOne = value.IsBitSet(4);
            if (SideOne != sideOne)
            {
                SideOne = sideOne;
                Log.LogDebug(string.Format("FDC Side select: {0}", sideOne ? 1 : 0));
            }

            if (value.IsBitSet(6))
                clock.Wait();

            DoubleDensitySelected = value.IsBitSet(7);

            if (MotorOn)
                MotorOnCallback();
            else
                computer.RegisterPulseReq(motorOnPulseReq);
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
                statusRegister |= 0x84; // not ready + track zero
            }
            else if (DriveIsUnloaded(CurrentDriveNumber))
            {
                // status remains zero
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
                        statusRegister |= 0x04;
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
                    if (DrqStatus)
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
                    if (DrqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.ReadTrack:
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (DrqStatus)
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
                    if (DrqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.WriteTrack:
                    if (writeProtected)
                        statusRegister |= 0x40; // Bit 6: Write Protect detect
                    if (LostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (DrqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
            }
            if (Busy)
                statusRegister |= 0x01; // Bit 0: Busy
        }

        // INTERRUPTS

        private void DoNmi()
        {
            Log.LogDebug(string.Format("NMI requested. Command: {0} Opstatus: {1}", command, opStatus));
            drq = DrqStatus = false;
            Busy = false;
            IntMgr.FdcNmiLatch.Latch();
        }
        private void SetDRQ()
        {
            drq = DrqStatus = true;
        }

        // SPIN SIMULATION

        private ulong DiskAngle
        {
            get { return DISK_ANGLE_DIVISIONS * (clock.TickCount % TicksPerDiskRev) / TicksPerDiskRev; }
        }
        public bool IndexDetect
        {
            get
            {
                var da = DiskAngle;
                bool indexDetect = MotorOn && DiskAngle > INDEX_PULSE_START && DiskAngle < INDEX_PULSE_END;

                return indexDetect;
            }
        }
        private int IndexesFound
        {
            get
            {
                UpdateIndexHoleStatus();
                return indexesFound;
            }
        }

        // TRACK DATA HANLDING

        public int TrackDataIndex
        {
            get
            {
                return (int)(DiskAngle * (ulong)(track?.DataLength ?? (DoubleDensitySelected ? Floppy.STANDARD_TRACK_LENGTH_DOUBLE_DENSITY : Floppy.STANDARD_TRACK_LENGTH_SINGLE_DENSITY)) / DISK_ANGLE_DIVISIONS);
            }
        }
        private byte ReadTrackByte()
        {
            return track?.ReadByte(TrackDataIndex, DoubleDensitySelected) ?? 0;
        }
        private void WriteTrackByte(byte B)
        {
            track?.WriteByte(TrackDataIndex, DoubleDensitySelected, B);
        }
        private void ResetIndexCount()
        {
            indexesFound = 0;
            indexFoundLastCheck = false;
        }
        private void UpdateIndexHoleStatus()
        {
            bool indexDetected = IndexDetect;

            if (indexDetected && !indexFoundLastCheck)
                indexesFound++;

            indexFoundLastCheck = indexDetected;
        }
        public void Dispose()
        {
            motorOffPulseReq?.Expire();
            motorOnPulseReq?.Expire();
            commandPulseReq?.Expire();
        }
    }
}