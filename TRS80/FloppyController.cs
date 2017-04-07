/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Linq;

namespace Sharp80.TRS80
{
    public partial class FloppyController : ISerializable, IFloppyControllerStatus
    {
        public const int NUM_DRIVES = 4;
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
        
        private PulseReq motorOffPulseReq;
        private PulseReq motorOnPulseReq;
        private PulseReq commandPulseReq;

        private Track track;

        // FDC Hardware Registers
        public byte CommandRegister => command.CommandRegister;
        public byte TrackRegister { get; private set; }
        public byte DataRegister { get; private set; }
        public byte SectorRegister { get; private set; }
        private byte StatusRegister { get; set; }

        // FDC Flags, etc.
        public bool Busy { get; private set; }
        public bool DoubleDensitySelected { get; private set; }
        public bool Drq { get; private set; }
        public bool CrcError { get; private set; }
        public bool LostData { get; private set; }
        public bool SeekError { get; private set; }
        private bool lastStepDirUp;
        private bool sectorDeleted;
        private bool writeProtected;

        // The physical state
        private byte currentDriveNumber = 0xFF;
        private bool sideOneSelected = false;
        public byte CurrentDriveNumber
        {
            get => currentDriveNumber;
            set
            {
                if (currentDriveNumber != value)
                {
                    currentDriveNumber = value;
                    UpdateTrack();
                }
            }
        }
        public bool SideOneSelected
        {
            get => sideOneSelected;
            private set
            {
                if (sideOneSelected != value)
                {
                    sideOneSelected = value;
                    UpdateTrack();
                }
            }
        }
        private DriveState[] drives;

        // Operation state
        private FdcCommand command;
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
        private Action nextCallback;
        private bool isPolling;
        private int targetDataIndex;
        private ulong TicksPerDiskRev { get; }

        // CONSTRUCTOR

        internal FloppyController(Computer Computer, PortSet Ports, Clock Clock, InterruptManager InterruptManager, ISound Sound, bool Enabled)
        {
            computer = Computer;
            ports = Ports;
            IntMgr = InterruptManager;
            clock = Clock;
            sound = Sound;
            this.Enabled = Enabled;

            TicksPerDiskRev = Clock.TICKS_PER_SECOND / DISK_REV_PER_SEC;

            drives = new DriveState[] { new DriveState(), new DriveState(), new DriveState(), new DriveState() };

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
        internal Floppy GetFloppy(int DriveNumber) => drives[DriveNumber].Floppy;
        private DriveState CurrentDrive => (CurrentDriveNumber >= NUM_DRIVES) ? null : drives[CurrentDriveNumber];

        // EXTERNAL INTERACTION AND INFORMATION

        internal void HardwareReset()
        {
            // This is a hardware reset, not an FDC command
            // We need to set the results as if a Restore command was completed.

            DataRegister = 0;
            TrackRegister = 0;
            SectorRegister = 0x01;
            StatusRegister = 0;

            command = new FdcCommand(0x03);

            Busy = false;
            Drq = false;
            SeekError = false;
            LostData = false;
            CrcError = false;
            sectorDeleted = false;
            writeProtected = false;

            lastStepDirUp = true;

            DoubleDensitySelected = false;

            motorOffPulseReq?.Expire();
            motorOnPulseReq?.Expire();
            commandPulseReq?.Expire();

            motorOnPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, MOTOR_ON_DELAY_IN_USEC, MotorOnCallback);
            motorOffPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, MOTOR_OFF_DELAY_IN_USEC, MotorOffCallback);
            commandPulseReq = new PulseReq();

            isPolling = false;
        }
        internal bool LoadFloppy(byte DriveNum, string FilePath)
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
                catch (Exception)
                {
                    ret = false;
                }
                if (f is null)
                    UnloadDrive(DriveNum);
                else
                    LoadDrive(DriveNum, f);
            }
            return ret;
        }
        internal void LoadFloppy(byte DriveNum, Floppy Floppy)
        {
            LoadDrive(DriveNum, Floppy);
        }
        internal bool SaveFloppy(byte DriveNum)
        {
            try
            {
                var f = drives[DriveNum].Floppy;

                if (!string.IsNullOrWhiteSpace(f.FilePath))
                    IO.SaveBinaryFile(f.FilePath, f.Serialize(ForceDMK: false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private void LoadDrive(byte DriveNum, Floppy Floppy)
        {
            drives[DriveNum].Floppy = Floppy;
            UpdateTrack();
        }
        internal void UnloadDrive(byte DriveNum)
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

        internal bool? DriveBusyStatus => MotorOn ? (bool?)Busy : null;

        internal bool AnyDriveLoaded => drives.Any(d => !d.IsUnloaded);
        internal bool Available => Enabled && !DriveIsUnloaded(0);
        internal void Disable() => Enabled = false;
        internal bool DriveIsUnloaded(byte DriveNum) => drives[DriveNum].IsUnloaded;
        internal string FloppyFilePath(byte DriveNum) => drives[DriveNum].Floppy?.FilePath ?? String.Empty;
        internal bool? DiskHasChanged(byte DriveNum) => drives[DriveNum].Floppy?.Changed;

        internal bool? IsWriteProtected(byte DriveNumber)
        {
            if (DriveIsUnloaded(DriveNumber))
                return null;
            else
                return drives[DriveNumber].WriteProtected;
        }
        internal void SetWriteProtection(byte DriveNumber, bool WriteProtected)
        {
            if (!DriveIsUnloaded(DriveNumber))
                drives[DriveNumber].WriteProtected = WriteProtected;
        }

        private void SeekAddressData(byte ByteRead, OpStatus NextStatus, bool Check)
        {
            damBytesChecked++;
            switch (opStatus)
            {
                case OpStatus.SeekingIDAM:
                    if (IndexesFound >= 5)
                    {
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

                        var match = (TrackRegister == readAddressData[ADDRESS_DATA_TRACK_REGISTER_BYTE] &&
                                     (!command.SideSelectVerify || (command.SideOneExpected == (readAddressData[ADDRESS_DATA_SIDE_ONE_BYTE] != 0))) &&
                                     (SectorRegister == readAddressData[ADDRESS_DATA_SECTOR_REGISTER_BYTE]));

                        if (Check && !match)
                        {
                            opStatus = OpStatus.SeekingIDAM;
                        }
                        else
                        {
                            sectorLength = Floppy.GetDataLengthFromCode(readAddressData[ADDRESS_DATA_SECTOR_SIZE_BYTE]);

                            if (CrcError)
                                opStatus = OpStatus.SeekingIDAM;
                            else
                                opStatus = NextStatus;
                        }
                    }
                    readAddressIndex++;
                    break;
                default:
                    throw new Exception();
            }
        }

        // TRACK ACTIONS

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
            }
        }

        // TRIGGERS

        private void SetCommandPulseReq(int Bytes, ulong DelayInUsec, Action Callback)
        {
            commandPulseReq.Expire();
            nextCallback = null;
            isPolling = false;

            if (Bytes <= 0 && DelayInUsec == 0)
            {
                Callback();
            }
            else if (Bytes > 0 && DelayInUsec > 0)
            {
                throw new Exception("Can't have both Byte and Time based delays");
            }
            else if (DelayInUsec == 0)
            {
                // Byte based delay

                if (!DoubleDensitySelected)
                    Bytes *= 2;

                // we want to come back exactly when the target byte is under the drive head
                targetDataIndex = (TrackDataIndex + Bytes) % TrackLength;
                if (!DoubleDensitySelected)
                    targetDataIndex &= 0xFFFE; // find the first of the doubled bytes

                nextCallback = Callback;
                isPolling = true;
                // Calculate how long it will take for the right byte to be under the head
                // just like the real life controller does.
                commandPulseReq = new PulseReq(PulseReq.DelayBasis.Ticks,
                                               ((((ulong)targetDataIndex * DISK_ANGLE_DIVISIONS / (ulong)TrackLength) + DISK_ANGLE_DIVISIONS) - DiskAngle) % DISK_ANGLE_DIVISIONS * TicksPerDiskRev / DISK_ANGLE_DIVISIONS + 10000,
                                               Poll);
                computer.RegisterPulseReq(commandPulseReq, true);
            }
            else
            {
                // Time based delay
                commandPulseReq = new PulseReq(PulseReq.DelayBasis.Microseconds, DelayInUsec, Callback);
                computer.RegisterPulseReq(commandPulseReq, true);
            }
        }
        private void Poll()
        {
            if (!isPolling)
            {
                throw new Exception("Polling error.");
            }
            else if (TrackDataIndex == targetDataIndex)
            {
                // this is the byte we're looking for
                isPolling = false;
                nextCallback();
            }
            else
            {
                System.Diagnostics.Debug.Assert(false, "Missed the target!");
                // we could cheat and wait for the target by calling Poll() again but that's not good emulation
                // so just behave as if there were a fault. This shouldn't happen unless switching floppings mid
                // read or write.
                isPolling = false;
                nextCallback();
            }
        }

        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(TrackRegister);
            Writer.Write(SectorRegister);
            Writer.Write(command.CommandRegister);
            Writer.Write(DataRegister);
            Writer.Write(StatusRegister);

            Writer.Write((int)0);  // not used

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

            // now unused vals
            Writer.Write((ulong)0);
            Writer.Write(false);
            Writer.Write(false);
            Writer.Write(false);
            Writer.Write(false);
            Writer.Write(false);
            Writer.Write(false);

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
            Writer.Write(sideOneSelected);
            Writer.Write(Enabled);

            commandPulseReq.Serialize(Writer);
            motorOnPulseReq.Serialize(Writer);
            motorOffPulseReq.Serialize(Writer);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int SerializationVersion)
        {
            try
            {
                bool ok = true;

                TrackRegister = Reader.ReadByte();
                SectorRegister = Reader.ReadByte();
                command = new FdcCommand(Reader.ReadByte());
                DataRegister = Reader.ReadByte();
                StatusRegister = Reader.ReadByte();

                Reader.ReadInt32(); // not used

                opStatus = (OpStatus)Reader.ReadInt32();

                DoubleDensitySelected = Reader.ReadBoolean();
                sectorDeleted = Reader.ReadBoolean();
                Busy = Reader.ReadBoolean();
                Drq = Reader.ReadBoolean();
                SeekError = Reader.ReadBoolean();
                CrcError = Reader.ReadBoolean();
                LostData = Reader.ReadBoolean();
                lastStepDirUp = Reader.ReadBoolean();
                writeProtected = Reader.ReadBoolean();
                MotorOn = Reader.ReadBoolean();

                // now unused vals
                Reader.ReadUInt64();
                Reader.ReadBoolean();
                Reader.ReadBoolean();
                Reader.ReadBoolean();
                Reader.ReadBoolean();
                Reader.ReadBoolean();
                Reader.ReadBoolean();

                Array.Copy(Reader.ReadBytes(ADDRESS_DATA_BYTES), readAddressData, ADDRESS_DATA_BYTES);

                readAddressIndex = Reader.ReadByte();
                idamBytesFound = Reader.ReadInt32();
                damBytesChecked = Reader.ReadInt32();
                sectorLength = Reader.ReadInt32();
                bytesRead = Reader.ReadInt32();
                bytesToWrite = Reader.ReadInt32();

                indexCheckStartTick = Reader.ReadUInt64();
                isPolling = Reader.ReadBoolean();
                targetDataIndex = Reader.ReadInt32();

                crc = Reader.ReadUInt16();
                crcCalc = Reader.ReadUInt16();
                crcHigh = Reader.ReadByte();
                crcLow = Reader.ReadByte();

                for (byte b = 0; b < NUM_DRIVES; b++)
                {
                    ok &= drives[b].Deserialize(Reader, SerializationVersion);
                    if (drives[b].IsLoaded)
                        if (System.IO.File.Exists(drives[b].Floppy.FilePath))
                            Storage.SaveDefaultDriveFileName(b, drives[b].Floppy.FilePath);
                }
                currentDriveNumber = Reader.ReadByte();
                sideOneSelected = Reader.ReadBoolean();
                if (SerializationVersion >= 10)
                    Enabled = Reader.ReadBoolean();
                else
                    Enabled = AnyDriveLoaded;

                Action callback;

                switch (command.Type)
                {
                    case FdcCommandType.ReadAddress:
                        callback = ReadAddressCallback;
                        break;
                    case FdcCommandType.ReadSector:
                        callback = ReadSectorCallback;
                        break;
                    case FdcCommandType.ReadTrack:
                        callback = ReadTrackCallback;
                        break;
                    case FdcCommandType.WriteSector:
                        callback = WriteSectorCallback;
                        break;
                    case FdcCommandType.WriteTrack:
                        callback = WriteTrackCallback;
                        break;
                    case FdcCommandType.Restore:
                    case FdcCommandType.Seek:
                    case FdcCommandType.Step:
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
                        computer.RegisterPulseReq(commandPulseReq, false);
                    if (motorOnPulseReq.Active)
                        computer.RegisterPulseReq(motorOnPulseReq, false);
                    if (motorOffPulseReq.Active)
                        computer.RegisterPulseReq(motorOffPulseReq, false);

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

        internal void FdcIoEvent(byte portNum, byte value, bool isOut)
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
            StatusRegister = command.GetStatus(MotorOn, Busy, IndexDetect, CurrentDrive.IsLoaded, writeProtected, Drq, sectorDeleted, SeekError, CrcError, LostData, CurrentDrive.OnTrackZero);

            if (Enabled)
                ports.SetPortDirect(0xF0, StatusRegister);
            else
                ports.SetPortDirect(0xF0, 0xFF);

            IntMgr.FdcNmiLatch.Unlatch();
            IntMgr.FdcMotorOffNmiLatch.Unlatch();
        }
        private void GetTrackRegister() => ports.SetPortDirect(0xF1, Enabled ? TrackRegister : (byte)0xFF);
        private void GetSectorRegister() => ports.SetPortDirect(0xF2, Enabled ? SectorRegister : (byte)0xFF);
        private void GetDataRegister()
        {
            ports.SetPortDirect(0xF3, Enabled ? DataRegister : (byte)0xFF);
            Drq = false;
        }

        private void SetCommandRegister(byte value)
        {            
            command = new FdcCommand(value);

            IntMgr.FdcNmiLatch.Unlatch();
            IntMgr.FdcMotorOffNmiLatch.Unlatch();

            if (!Busy || (command.Type != FdcCommandType.Reset))
            {
                Drq = false;
                LostData = false;
                SeekError = false;
                CrcError = false;
                writeProtected = CurrentDrive.WriteProtected;
                sectorDeleted = false;
                Busy = true;
            }
            opStatus = OpStatus.Prepare;
            idamBytesFound = 0;

            switch (command.Type)
            {
                case FdcCommandType.Restore:
                    TrackRegister = 0xFF;
                    DataRegister = 0x00;
                    TypeOneCommandCallback();
                    break;
                case FdcCommandType.Seek:
                    TypeOneCommandCallback();
                    break;
                case FdcCommandType.Step:
                    if (value.IsBitSet(6))
                        if (value.IsBitSet(5))
                            lastStepDirUp = false;
                        else
                            lastStepDirUp = true;
                    TypeOneCommandCallback();
                    break;
                case FdcCommandType.ReadSector:
                    ReadSectorCallback();
                    break;
                case FdcCommandType.WriteSector:
                    WriteSectorCallback();
                    break;
                case FdcCommandType.ReadAddress:
                    ReadAddressCallback();
                    break;
                case FdcCommandType.Reset:
                    // puts FDC in mode 1
                    commandPulseReq.Expire();
                    Busy = false;
                    opStatus = OpStatus.OpDone;
                    break;
                case FdcCommandType.ForceInterruptImmediate:
                case FdcCommandType.ForceInterrupt:
                    DoNmi();
                    opStatus = OpStatus.OpDone;
                    break;
                case FdcCommandType.ReadTrack:
                    ReadTrackCallback();
                    break;
                case FdcCommandType.WriteTrack:
                    WriteTrackCallback();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        private void SetTrackRegister(byte value)
        {
            TrackRegister = value;
        }
        private void SetSectorRegister(byte value)
        {
            SectorRegister = value;
        }
        private void SetDataRegister(byte value)
        {
            DataRegister = value;
            Drq = false;
        }
        private void FdcDiskSelect(byte Value)
        {
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

            SideOneSelected = Value.IsBitSet(4);

            if (Value.IsBitSet(6))
                clock.Wait();

            DoubleDensitySelected = Value.IsBitSet(7);

            if (MotorOn)
                MotorOnCallback();
            else
                computer.RegisterPulseReq(motorOnPulseReq, true);
        }
        
        // INTERRUPTS

        private void DoNmi()
        {
            Drq = false;
            Busy = false;
            IntMgr.FdcNmiLatch.Latch();
        }
        private void SetDRQ() => Drq = true;

        // SPIN SIMULATION

        private ulong DiskAngle => DISK_ANGLE_DIVISIONS * (clock.TickCount % TicksPerDiskRev) / TicksPerDiskRev;
        public bool IndexDetect => MotorOn && DiskAngle < INDEX_PULSE_END;
        public int IndexesFound => (int)((clock.TickCount - indexCheckStartTick) / TicksPerDiskRev);
        public void ResetIndexCount()
        {
            // make as if we started checking just after the index pulse started
            indexCheckStartTick = clock.TickCount - (clock.TickCount % TicksPerDiskRev) + 10;
        }

        // TRACK DATA HANLDING

        public int TrackDataIndex => (int)(DiskAngle * (ulong)TrackLength  / DISK_ANGLE_DIVISIONS);
        private int TrackLength => track?.DataLength ??
                        (DoubleDensitySelected ? Floppy.STANDARD_TRACK_LENGTH_DOUBLE_DENSITY : Floppy.STANDARD_TRACK_LENGTH_SINGLE_DENSITY);
        private byte ReadTrackByte() => track?.ReadByte(TrackDataIndex, DoubleDensitySelected) ?? 0;
        private void WriteTrackByte(byte B) => track?.WriteByte(TrackDataIndex, DoubleDensitySelected, B);

        // HELPERS
        
        internal static ushort UpdateCRC(ushort crc, byte ByteRead, bool AllowReset, bool DoubleDensity)
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

        // SHUTDOWN

        public void Shutdown()
        {
            motorOffPulseReq?.Expire();
            motorOnPulseReq?.Expire();
            commandPulseReq?.Expire();
        }
    }
}