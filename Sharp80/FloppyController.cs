/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Linq;
using System.IO;

namespace Sharp80
{
    internal sealed class FloppyController : ISerializable
    {
        public const int NUM_DRIVES = 4;

        private sealed class DriveState
        {
            public Floppy Floppy { get; set; }
            public bool IsLoaded { get { return Floppy != null; } }
            public bool IsUnloaded { get { return Floppy == null; } }
            public bool OnTrackZero { get { return PhysicalTrackNumber == 0; } }
            public byte PhysicalTrackNumber
            {
                get; set;
            }
            public bool WriteProtected { get { return Floppy?.WriteProtected ?? true; } set { Floppy.WriteProtected = value; } }
            public DriveState()
            {
                PhysicalTrackNumber = 0;
                Floppy = null;
            }
            public void Serialize(BinaryWriter Writer)
            {
                Writer.Write(Floppy != null);
                if (Floppy != null)
                    Floppy.Serialize(Writer);
                Writer.Write(PhysicalTrackNumber);
            }
            public void Deserialize(BinaryReader Reader)
            {
                if (Reader.ReadBoolean())
                    Floppy = new DMK(Reader);
                else
                    Floppy = null;
                PhysicalTrackNumber = Reader.ReadByte();
            }
        }
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
        private const ulong DISK_ANGLE_DIVISIONS = 1000000ul;            // measure billionths of a rotation
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
        private const ulong TOTAL_BYTES_ON_DD_TRACK = 6250;
        private const ulong TOTAL_BYTES_ON_SD_TRACK = 3500; // TODO: check this
        private const ulong BYTE_TIME_IN_USEC = 8 * SECONDS_TO_MICROSECONDS / 250000; // 250KHz in MFM
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

        private ulong stepRateInUsec;
        private readonly ulong[] stepRates;
        private bool verify;
        private bool delay;
        private bool updateRegisters;
        private bool sideSelectVerify;
        private bool sideOneExpected;
        private bool markSectorDeleted;
        private bool multipleRecords;
        private bool motorOn;
        private PulseReq motorOffPulseReq;
        private PulseReq motorOnPulseReq;
        private PulseReq commandPulseReq;

        private Track track;
        private int trackDataIndex;

        // FDC Hardware Registers
        private byte commandRegister;
        private byte dataRegister;
        private byte trackRegister;
        private byte sectorRegister;
        private byte statusRegister;

        // FDC Flags, etc.
        private bool busy;
        private bool drq;
        private bool drqStatus;
        private bool seekErrorOrRecordNotFound;
        private bool crcError;
        private bool lostData;
        private bool lastStepDirUp;
        private bool doubleDensitySelected;
        private bool sectorDeleted;
        private bool writeProtected;

        // The physical state
        private DriveState[] drives;
        private DriveState currentDrive { get { return (CurrentDriveNumber >= NUM_DRIVES) ? null : drives[CurrentDriveNumber]; } }
        private byte currentDriveNumber = 0xFF;
        private byte CurrentDriveNumber
        {
            get { return currentDriveNumber; }
            set
            {
                if (currentDriveNumber != value)
                {
                    currentDriveNumber = value;
                    //currentDrive = drives[value];
                    UpdateTrack();
                }
            }
        }

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

        public FloppyController(Computer Computer, PortSet Ports, Clock Clock, InterruptManager InterruptManager, ISound Sound)
        {
            computer = Computer;
            ports = Ports;
            IntMgr = InterruptManager;
            clock = Clock;
            sound = Sound;

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

            motorOn = false;
        }

        // PUBLIC INTERFACE

        public FloppyControllerStatus GetStatus()
        {
            var tdi = (int)(DiskAngle * (ulong)(track?.DataLength ?? Track.DEFAULT_LENGTH_WITHOUT_HEADER) / DISK_ANGLE_DIVISIONS);

            var s = new FloppyControllerStatus()
            {
                OpStatus = opStatus.ToString(),
                Busy = busy,
                CommandStatus = command.ToString(),
                TrackRegister = trackRegister,
                SectorRegister = sectorRegister,
                CommandRegister = commandRegister,
                DataRegister = dataRegister,
                DiskNum = CurrentDriveNumber,
                PhysicalTrackNum = currentDrive.PhysicalTrackNumber,
                DiskAngle = ((double)DiskAngle / DISK_ANGLE_DIVISIONS * 360).ToString("000.000") + " degrees",
                TrackDataIndex = tdi,
                ByteAtTrackDataIndex = track?.ReadByte(tdi, null) ?? 0,
                IndexHole = IndexDetect,
                DoubleDensity = doubleDensitySelected,
                DRQ = drqStatus,
                SeekError = seekErrorOrRecordNotFound,
                LostData = lostData,
                CrcError = crcError
            };

            return s;
        }
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

            commandRegister = 0x03;
            dataRegister = 0;
            trackRegister = 0;
            sectorRegister = 0x01;
            statusRegister = 0;

            command = Command.Restore;

            busy = false;
            drq = drqStatus = false;
            seekErrorOrRecordNotFound = false;
            lostData = false;
            crcError = false;
            sectorDeleted = false;
            writeProtected = false;

            lastStepDirUp = true;

            sideOneExpected = false;
            sideSelectVerify = false;
            doubleDensitySelected = false;

            if (motorOffPulseReq != null)
                motorOffPulseReq.Expire();
            if (motorOnPulseReq != null)
                motorOnPulseReq.Expire();
            if (commandPulseReq != null)
                commandPulseReq.Expire();

            motorOnPulseReq = new PulseReq(MOTOR_ON_DELAY_IN_USEC, MotorOnCallback, true);
            motorOffPulseReq = new PulseReq(MOTOR_OFF_DELAY_IN_USEC, MotorOffCallback, true);
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
                Log.Log(string.Format("File {0} does not exist. Load cancelled.", FilePath));
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
                    Log.Log("Can't save floppy without file path.");
                else
                    Storage.SaveBinaryFile(f.FilePath, f.Serialize(ForceDMK: false));
            }
            catch (Exception ex)
            {
                Log.Log(string.Format("Error saving files {0}: {1}", DriveNum, ex));
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

        public string GetDriveStatusReport()
        {
            return "D" + CurrentDriveNumber.ToString() +
                   ":S" + (SideOne ? "1" : "0") +
                   ":T" + currentDrive.PhysicalTrackNumber.ToString("00") +
                   ":S" + sectorRegister.ToString("00");
        }

        public bool? DriveBusyStatus
        {
            get
            {
                if (busy)
                    return true;
                else if (motorOn)
                    return false;
                else
                    return null;
            }
        }
        public bool AnyDriveLoaded
        {
            get { return drives.Any(d => !d.IsUnloaded); }
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
                    verify = commandRegister.IsBitSet(2);
                    stepRateInUsec = stepRates[commandRegister & 0x03];
                    updateRegisters = command == Command.Seek || command == Command.Restore || commandRegister.IsBitSet(4);
                    opStatus = OpStatus.Step;
                    break;
                case OpStatus.Step:

                    if (command == Command.Seek || command == Command.Restore)
                    {
                        if (dataRegister == trackRegister)
                            opStatus = OpStatus.CheckVerify;
                        else
                            lastStepDirUp = (dataRegister > trackRegister);
                    }
                    if (opStatus == OpStatus.Step)
                    {
                        if (updateRegisters)
                            trackRegister = lastStepDirUp ? (byte)Math.Min(MAX_TRACKS, trackRegister + 1)
                                                          : (byte)Math.Max(0, trackRegister - 1);

                        if (lastStepDirUp)
                            StepUp();
                        else
                            StepDown();

                        if (currentDrive.OnTrackZero && !lastStepDirUp)
                        {
                            trackRegister = 0;
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

                    if (currentDrive.PhysicalTrackNumber != trackRegister)
                        Log.Log(string.Format("Track Register {0} != Physical Track Number {1}", trackRegister, currentDrive.PhysicalTrackNumber));

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
                    if (trackRegister == readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE])
                    {
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        Log.Log(string.Format(opStatus.ToString() + " Verify Track failed: Track Register: {0} Track Read {1}", trackRegister, readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]));
                        delayBytes = 1;
                        opStatus = OpStatus.SeekingIDAM;
                    }
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (busy)
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

                    sideSelectVerify =commandRegister.IsBitSet(1);
                    sideOneExpected = commandRegister.IsBitSet(3);
                    multipleRecords = commandRegister.IsBitSet(4);

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
                    if (damBytesChecked++ > (doubleDensitySelected ? 43 : 30))
                    {
                        Log.Log(string.Format("Error: Seek Error / Record Not Found. Command: {0} OpStatus: {1}", command, opStatus));
                        seekErrorOrRecordNotFound = true;
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
                        Log.Log(string.Format("Error: Lost Data. Command: {0} OpStatus: {1} TrackRegister: {2} SectorRegister {3} Bytes Read: {4}",
                                                      command, opStatus, trackRegister, sectorRegister, bytesRead));
                        lostData = true;
                    }
                    if (++bytesRead >= sectorLength)
                    {
                        crcCalc = crc;
                        opStatus = OpStatus.ReadCRCHigh;
                    }
                    Log.Log(string.Format("FDC Write to data register: {0}", b.ToHexString()));
                    dataRegister = b;
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
                    crcError = crcCalc != Lib.CombineBytes(crcLow, crcHigh);
                    // crcError = crc != 0x0000;
                    if (crcError)
                    {
                        Log.Log(string.Format("Error: CRC Error. Command: {0} OpStatus: {1}", command, opStatus));
                        Log.Log(string.Format("Data CRC Error - Drv: {0} Trk: {1} Side: {2} Sec: {3}", CurrentDriveNumber, trackRegister, sideOneExpected ? 1 : 0, sectorRegister));
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
                        sectorRegister++;
                        delayBytes = 1;
                        opStatus = OpStatus.SeekingIDAM;
                    }

                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (busy)
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

                    sideOneExpected =   commandRegister.IsBitSet(3);
                    delay =             commandRegister.IsBitSet(2);
                    sideSelectVerify =  commandRegister.IsBitSet(1);
                    markSectorDeleted = commandRegister.IsBitSet(0);

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
                    if (currentDrive.WriteProtected)
                    {
                        Log.Log(string.Format("Cancelling due to Write Protect: Command: {0} OpStatus: {1}", command, opStatus));

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
                        Log.Log(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
                        lostData = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    else
                    {
                        opStatus = OpStatus.WriteFiller;
                        delayBytes = doubleDensitySelected ? 12 : 1;
                        bytesToWrite = doubleDensitySelected ? 12 : 6;
                    }
                    break;
                case OpStatus.WriteFiller:
                    WriteByte(0x00, false);
                    if (--bytesToWrite == 0)
                        opStatus = OpStatus.WriteDAM;
                    delayBytes = 1;
                    break;
                case OpStatus.WriteDAM:
                    opStatus = OpStatus.WritingData;
                    ResetCRC();
                    if (markSectorDeleted)
                        WriteByte(Floppy.DAM_DELETED, true);
                    else
                        WriteByte(Floppy.DAM_NORMAL, true);
                    bytesToWrite = sectorLength;
                    delayBytes = 1;
                    break;
                case OpStatus.WritingData:
                    if (drq)
                    {
                        Log.Log(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
                        lostData = true;
                        WriteByte(0x00, false);
                    }
                    else
                    {
                        WriteByte(dataRegister, false);
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
                        //UpdateTrackData();
                        delayTime = NMI_DELAY_IN_USEC;
                    }
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;

            }
            if (busy)
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

                    delay = commandRegister.IsBitSet(2);

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
                    dataRegister = b;
                    SetDRQ();
                    SeekAddressData(b, OpStatus.Finalize, false);
                    break;
                case OpStatus.Finalize:
                    // Error in doc? from the doc: "The Track Address of the ID field is written
                    // into the sector register so that a comparison can be made
                    // by the user"
                    trackRegister = readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE];
                    sectorRegister = readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE];
                    opStatus = OpStatus.NMI;
                    delayTime = NMI_DELAY_IN_USEC;
                    break;
                case OpStatus.NMI:
                    opStatus = OpStatus.OpDone;
                    DoNmi();
                    break;
            }
            if (busy)
                SetCommandPulseReq(delayBytes, delayTime, ReadAddressCallback);
        }
        private void WriteTrackCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 1;

            switch (opStatus)
            {
                case OpStatus.Prepare:
                    SyncTrackDataIndexOnNextIndexDetect();
                    ResetIndexCount();

                    if (delay)
                        opStatus = OpStatus.Delay;
                    else
                        opStatus = OpStatus.CheckingWriteProtectStatus;

                    delayTime = 100;

                    break;
                case OpStatus.Delay:
                    opStatus = OpStatus.CheckingWriteProtectStatus;
                    delayTime = STANDARD_DELAY_TIME_IN_USEC;
                    delayBytes = 0;
                    break;
                case OpStatus.CheckingWriteProtectStatus:
                    if (currentDrive.WriteProtected)
                    {
                        Log.Log(string.Format("Cancelling due to Write Protect: Command: {0} OpStatus: {1}", command, opStatus));

                        writeProtected = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
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
                        Log.Log(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
                        lostData = true;
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
                    byte b = dataRegister;
                    bool doDrq = true;
                    bool allowCrcReset = true;
                    if (drq)
                    {
                        Log.Log(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
                        lostData = true;
                        b = 0x00;
                    }
                    else
                    {
                        if (doubleDensitySelected)
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
                        //ResizeTrack((int)trackDataIndex);
                        //UpdateTrackData();
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
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
            if (busy)
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

                    SyncTrackDataIndexOnNextIndexDetect();
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
                    dataRegister = b;
                    if (drq)
                    {
                        Log.Log(string.Format("Error: Lost Data. Command: {0} OpStatus: {1}", command, opStatus));
                        lostData = true;
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
            if (busy)
                SetCommandPulseReq(delayBytes, delayTime, ReadTrackCallback);
        }
        // TODO: Events for these
        private void MotorOnCallback()
        {
            motorOn = true;
            sound.DriveMotorRunning = true;
            clock.RegisterPulseReq(motorOffPulseReq);
        }
        private void MotorOffCallback()
        {
            Log.Log("FDC Motor Off");

            motorOn = false;
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
                        Log.Log(string.Format("Error: Seek Error / Record Not Found. Command: {0} OpStatus: {1} Track Register: {2} Sector Register: {3} Side Sel Verify: {4} Indexes Found: {5}", command, opStatus, trackRegister, sectorRegister, sideSelectVerify, IndexesFound));
                        seekErrorOrRecordNotFound = true;
                        opStatus = OpStatus.NMI;
                    }
                    else
                    {
                        switch (ByteRead)
                        {
                            case 0xA1:
                                if (doubleDensitySelected)
                                    idamBytesFound++;
                                break;
                            case 0x00:
                                if (!doubleDensitySelected)
                                    idamBytesFound++;
                                break;
                            case Floppy.IDAM:
                                //if ((doubleDensitySelected && idamBytesFound >= 3) || (!doubleDensitySelected && idamBytesFound >= 1))
                                if (idamBytesFound >= 1)
                                {
                                    Log.Log(string.Format("IDAM Found. Command: {0} OpStatus: {1} Track Register: {2} Sector Register: {3} Side Sel Verify: {4} Indexes Found: {5}", command, opStatus, trackRegister, sectorRegister, sideSelectVerify, IndexesFound));
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
                        crcError = crcCalc != Lib.CombineBytes(crcLow, crcHigh);

                        if (Check && (trackRegister != readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE] ||
                                     (sideSelectVerify && (sideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 0))) ||
                                     (sectorRegister != readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE])))
                        {
                            opStatus = OpStatus.SeekingIDAM;
#if DEBUG
                            Log.Log(string.Format("Address data not matching, continuing read. {0}{1}{2}",
                                (trackRegister != readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE]) ? "Track Register: " + trackRegister.ToString() + " Track Read: " + readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE].ToString() : "",
                                (sideSelectVerify && (sideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 0))) ? "Side expected: " + (sideOneExpected ? "1" : "0") + " Side Read: " + (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] == 1 ? "1" : "0") : "",
                                (sectorRegister != readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE]) ? " Sector Register: " + sectorRegister.ToString() + " Sector Read: " + readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE].ToString() : ""));
#endif
                        }
                        else
                        {
                            
                            Log.Log(string.Format("Correct Address Found. Command: {0} OpStatus: {1} " +
                                                            "Track Register: {2} Sector Register: {3} Side Sel Verify: {4} Indexes Found: {5}",
                                                            command, opStatus, trackRegister, sectorRegister, sideSelectVerify, IndexesFound));

                            sectorLength = Floppy.GetDataLengthFromCode(readAddressData[ADDRESS_DATA_SECTOR_SIZE_BYTE]);
                            
                            if (crcError)
                            {
                                Log.Log(string.Format("Address CRC Error - Drv: {0} Trk: {1} Side: {2} Sec: {3}",
                                    CurrentDriveNumber, trackRegister, sideOneExpected ? 1 : 0, sectorRegister));
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
            SetTrackNumber(currentDrive.PhysicalTrackNumber + 1);
        }
        private void StepDown()
        {
            SetTrackNumber(currentDrive.PhysicalTrackNumber - 1);
        }
        private void UpdateTrack()
        {
            track = currentDrive.Floppy?.GetTrack(currentDrive.PhysicalTrackNumber, SideOne);
        }
        private void SetTrackNumber(int TrackNum)
        {
            byte trackNum = (byte)(Math.Max(0, Math.Min(MAX_TRACKS, TrackNum)));

            if (currentDrive.PhysicalTrackNumber != trackNum)
            {
                currentDrive.PhysicalTrackNumber = trackNum;
                UpdateTrack();
                sound.TrackStep();
                if (currentDrive.OnTrackZero)
                    trackRegister = 0;

                Log.Log(string.Format("Drive {0} Physical Track Step to {1}",
                                                CurrentDriveNumber, currentDrive.PhysicalTrackNumber));
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

            int bytesToAdvance = Bytes + (int)(DelayInUsec / BYTE_TIME_IN_USEC);
            ulong delayTime = DelayInUsec + (ulong)Bytes * BYTE_TIME_IN_USEC;

            if (!doubleDensitySelected)
            {
                bytesToAdvance *= 2;
                delayTime *= 2;
            }

            if (delayTime > 0)
            {
                Log.Log(string.Format("Callback Request. Command: {0} Opstatus: {1}", command, opStatus));
                commandPulseReq = new PulseReq(delayTime, Callback, false);
                computer.RegisterPulseReq(commandPulseReq);
                trackDataIndex += bytesToAdvance;
            }
            else
            {
                Callback();
            }
        }

        // SNAPSHOTS

        public void Serialize(BinaryWriter Writer)
        {
            Writer.Write(trackRegister);
            Writer.Write(sectorRegister);
            Writer.Write(commandRegister);
            Writer.Write(dataRegister);
            Writer.Write(statusRegister);

            Writer.Write((int)command);
            Writer.Write((int)opStatus);

            Writer.Write(doubleDensitySelected);
            Writer.Write(sectorDeleted);
            Writer.Write(busy);
            Writer.Write(drq);
            Writer.Write(drqStatus);
            Writer.Write(seekErrorOrRecordNotFound);
            Writer.Write(crcError);
            Writer.Write(lostData);
            Writer.Write(lastStepDirUp);
            Writer.Write(writeProtected);
            Writer.Write(motorOn);
            Writer.Write(stepRateInUsec);
            
            Writer.Write(verify);
            Writer.Write(updateRegisters);
            Writer.Write(sideSelectVerify);
            Writer.Write(sideOneExpected);
            Writer.Write(markSectorDeleted);
            Writer.Write(multipleRecords);

            Writer.Write(trackDataIndex);
            
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
            trackRegister = Reader.ReadByte();
            sectorRegister = Reader.ReadByte();
            commandRegister = Reader.ReadByte();
            dataRegister = Reader.ReadByte();
            statusRegister = Reader.ReadByte();

            command = (Command)Reader.ReadInt32();
            opStatus = (OpStatus)Reader.ReadInt32();

            doubleDensitySelected = Reader.ReadBoolean();
            sectorDeleted = Reader.ReadBoolean();
            busy = Reader.ReadBoolean();
            drq = Reader.ReadBoolean();
            drqStatus = Reader.ReadBoolean();
            seekErrorOrRecordNotFound = Reader.ReadBoolean();
            crcError = Reader.ReadBoolean();
            lostData = Reader.ReadBoolean();
            lastStepDirUp = Reader.ReadBoolean();
            writeProtected = Reader.ReadBoolean();
            motorOn = Reader.ReadBoolean();
            stepRateInUsec = Reader.ReadUInt64();
            verify = Reader.ReadBoolean();
            updateRegisters = Reader.ReadBoolean();
            sideSelectVerify = Reader.ReadBoolean();
            sideOneExpected = Reader.ReadBoolean();
            markSectorDeleted = Reader.ReadBoolean();
            multipleRecords = Reader.ReadBoolean();

            trackDataIndex = Reader.ReadInt32();

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
            if (!commandPulseReq.Expired)
                computer.RegisterPulseReq(commandPulseReq);

            motorOnPulseReq.Deserialize(Reader, MotorOnCallback);
            if (!motorOnPulseReq.Expired)
                computer.RegisterPulseReq(motorOnPulseReq);

            motorOffPulseReq.Deserialize(Reader, MotorOffCallback);
            if (!motorOffPulseReq.Expired)
                computer.RegisterPulseReq(motorOffPulseReq);

            UpdateTrack();
        }

        private byte ReadByte(bool AllowResetCRC)
        {
            byte b = ReadTrackByte();

            if (AllowResetCRC)
                crc = UpdateCRC(crc, b, true, doubleDensitySelected);
            else
                crc = Lib.Crc(crc, b);

            return b;
        }
        private void WriteByte(byte B, bool AllowResetCRC)
        {
            if (AllowResetCRC)
                crc = UpdateCRC(crc, B, true, doubleDensitySelected);
            else
                crc = Lib.Crc(crc, B);

            WriteTrackByte(B);
        }
        private void ResetCRC()
        {
            crc = doubleDensitySelected ? Floppy.CRC_RESET_A1_A1_A1 : Floppy.CRC_RESET;
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

            Log.Log(string.Format("Get status register: {0}", statusRegister.ToHexString()));

            ports.SetPortDirect(statusRegister, 0xF0);

            IntMgr.FdcNmiLatch.Unlatch();
            IntMgr.FdcMotorOffNmiLatch.Unlatch();
        }
        private void GetTrackRegister()
        {
            ports.SetPortDirect(trackRegister, 0xF1);
            Log.Log(string.Format("Get track register: {0}", trackRegister.ToHexString()));
        }
        private void GetSectorRegister()
        {
            ports.SetPortDirect(sectorRegister, 0xF2);
            Log.Log(string.Format("Get sector register: {0}", sectorRegister.ToHexString()));
        }
        private void GetDataRegister()
        {
            Log.Log(string.Format("Read data register: {0}", dataRegister.ToHexString()));
            ports.SetPortDirect(dataRegister, 0xF3);
            drq = drqStatus = false;
        }
        private void SetCommandRegister(byte value)
        {
            command = GetCommand(value);

            Log.Log(string.Format("Setting command register: FDC Command {0} - Drv {1} [{2}]",
                                            command, CurrentDriveNumber, value.ToHexString()));

            try
            {
                commandRegister = value;

                IntMgr.FdcNmiLatch.Unlatch();
                IntMgr.FdcMotorOffNmiLatch.Unlatch();

                if (!busy || (command != Command.Reset))
                {
                    drq = drqStatus = false;
                    lostData = false;
                    seekErrorOrRecordNotFound = false;
                    crcError = false;
                    writeProtected = currentDrive.WriteProtected;
                    sectorDeleted = false;
                    busy = true;
                }

                if (currentDrive.IsUnloaded && command != Command.Reset)
                {
                    switch (command)
                    {
                        case Command.Seek:
                        default:
                            seekErrorOrRecordNotFound = true;
                            DoNmi();
                            return;
                    }
                }

                opStatus = OpStatus.Prepare;
                idamBytesFound = 0;

                switch (command)
                {
                    case Command.Restore:
                        trackRegister = 0xFF;
                        dataRegister = 0x00;
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
                        delay = commandRegister.IsBitSet(2);
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
                        busy = false;
                        opStatus = OpStatus.OpDone;
                        break;
                    case Command.ForceInterruptImmediate:
                        DoNmi();
                        opStatus = OpStatus.OpDone;
                        break;
                    case Command.ForceInterrupt:
                        Log.Log(string.Format("Unimplemented FDC Command: {0} [{1}]",
                                                        command.ToString(),
                                                        commandRegister.ToHexString()));
                        DoNmi();
                        opStatus = OpStatus.OpDone;
                        break;
                    case Command.ReadTrack:
                        delay = commandRegister.IsBitSet(2);
                        ReadTrackCallback();
                        break;
                    case Command.WriteTrack:
                        delay = commandRegister.IsBitSet(2);
                        WriteTrackCallback();
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            catch (Exception ex)
            {
                Log.LogException(ex);
                Log.Log(string.Format("Error setting command register: {0} FDC Command: Drv {1} - {2} [{3}]", ex, CurrentDriveNumber, command, value.ToHexString()));
            }
        }
        private void SetTrackRegister(byte value)
        {
            trackRegister = value;
            Log.Log(string.Format("Set track register: {0}", trackRegister.ToHexString()));
        }
        private void SetSectorRegister(byte value)
        {
            sectorRegister = value;
            Log.Log(string.Format("Set sector register: {0}", sectorRegister.ToHexString()));
        }
        private void SetDataRegister(byte value)
        {
            dataRegister = value;
            drq = drqStatus = false;
            Log.Log(string.Format("Set data register: {0}", dataRegister.ToHexString()));
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
                Log.Log(string.Format("FDC Disk select: {0}", floppyNum.Value.ToHexString()));
                CurrentDriveNumber = floppyNum.Value;
            }

            bool sideOne = value.IsBitSet(4);
            if (this.SideOne != sideOne)
            {
                this.SideOne = sideOne;
                Log.Log(string.Format("FDC Side select: {0}", sideOne ? 1 : 0));
            }

            if (value.IsBitSet(6))
                clock.Wait();

            doubleDensitySelected = value.IsBitSet(7);

            if (motorOn)
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
            if (!motorOn)
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
                        statusRegister |= 0x20;       // Bit 5: head loaded and engaged
                    if (seekErrorOrRecordNotFound)
                        statusRegister |= 0x10;   // Bit 4: Seek error
                    if (crcError)
                        statusRegister |= 0x08;   // Bit 3: CRC Error
                    if (currentDrive.OnTrackZero) // Bit 2: Track Zero detect
                        statusRegister |= 0x04;
                    if (indexHole)
                        statusRegister |= 0x02;   // Bit 1: Index Detect
                    break;
                case Command.ReadAddress:
                    if (seekErrorOrRecordNotFound)
                        statusRegister |= 0x10; // Bit 4: Record Not found
                    if (crcError)
                        statusRegister |= 0x08; // Bit 3: CRC Error
                    if (lostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (drqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.ReadSector:
                    if (sectorDeleted)
                        statusRegister |= 0x20; // Bit 5: Detect "deleted" address mark
                    if (seekErrorOrRecordNotFound)
                        statusRegister |= 0x10; // Bit 4: Record Not found
                    if (crcError)
                        statusRegister |= 0x08; // Bit 3: CRC Error
                    if (lostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (drqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.ReadTrack:
                    if (lostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (drqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.WriteSector:
                    if (writeProtected)
                        statusRegister |= 0x40; // Bit 6: Write Protect detect
                    if (seekErrorOrRecordNotFound)
                        statusRegister |= 0x10; // Bit 4: Record Not found
                    if (crcError)
                        statusRegister |= 0x08; // Bit 3: CRC Error
                    if (lostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (drqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
                case Command.WriteTrack:
                    if (writeProtected)
                        statusRegister |= 0x40; // Bit 6: Write Protect detect
                    if (lostData)
                        statusRegister |= 0x04; // Bit 2: Lost Data
                    if (drqStatus)
                        statusRegister |= 0x02; // Bit 1: DRQ
                    break;
            }
            if (busy)
                statusRegister |= 0x01; // Bit 0: Busy

            // TODO: should these be here?
            // drqStatus = false;
            // seekErrorOrRecordNotFound = false;
            //  lostData = false;
        }

        // INTERRUPTS

        private void DoNmi()
        {
            Log.Log(string.Format("NMI requested. Command: {0} Opstatus: {1}", command, opStatus));
            drq = drqStatus = false;
            busy = false;
            IntMgr.FdcNmiLatch.Latch();
        }
        private void SetDRQ()
        {
            drq = drqStatus = true;
        }

        // SPIN SIMULATION

        private ulong DiskAngle
        {
            get { return DISK_ANGLE_DIVISIONS * (clock.TickCount % TicksPerDiskRev) / TicksPerDiskRev; }
        }
        private bool IndexDetect
        {
            get
            {
                var da = DiskAngle;
                bool indexDetect = motorOn && DiskAngle > INDEX_PULSE_START && DiskAngle < INDEX_PULSE_END;

                if (indexDetect && syncOnNext)
                {
                    syncOnNext = false;
                    SyncTrackDataIndex();
                }

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

        private int TrackDataIndex
        {
            get
            {
                trackDataIndex %= track?.DataLength ?? Track.DEFAULT_LENGTH_WITHOUT_HEADER;
                return trackDataIndex;
            }
        }
        private byte ReadTrackByte()
        {
            return track?.ReadByte(TrackDataIndex, doubleDensitySelected) ?? 0;
        }
        private void WriteTrackByte(byte B)
        {
            track.WriteByte(TrackDataIndex, doubleDensitySelected, B);
        }
        private bool syncOnNext = false;
        private void SyncTrackDataIndexOnNextIndexDetect()
        {
            syncOnNext = true;
        }
        private void SyncTrackDataIndex()
        {
            trackDataIndex = (int)(DiskAngle * (ulong)track.DataLength / DISK_ANGLE_DIVISIONS);
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
    }
}