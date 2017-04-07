using System;

namespace Sharp80.TRS80
{
    public partial class FloppyController
    {
        // CALLBACKS

        private void TypeOneCommandCallback()
        {
            ulong delayTime = 0;
            int delayBytes = 0;

            switch (opStatus)
            {
                case OpStatus.Prepare:
                    opStatus = OpStatus.Step;
                    break;
                case OpStatus.Step:
                    if (command.Type == FdcCommandType.Seek || command.Type == FdcCommandType.Restore)
                    {
                        if (DataRegister == TrackRegister)
                            opStatus = OpStatus.CheckVerify;
                        else
                            lastStepDirUp = (DataRegister > TrackRegister);
                    }
                    if (opStatus == OpStatus.Step)
                    {
                        if (command.Type == FdcCommandType.Seek || command.Type == FdcCommandType.Restore || command.UpdateTrackRegister)
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
                        else if (command.Type == FdcCommandType.Step)
                        {
                            opStatus = OpStatus.CheckVerify;
                        }
                        delayTime = command.StepRate;
                    }
                    break;
                case OpStatus.CheckVerify:
                    if (command.TypeOneVerify)
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

                    if (command.Delay)
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
                        SeekError = true;
                        opStatus = OpStatus.NMI;
                        delayTime = NMI_DELAY_IN_USEC;
                        delayBytes = 0;
                    }
                    else if (IsDAM(b, out sectorDeleted))
                    {
                        ResetCRC();
                        crc = Lib.Crc(crc, b);
                        opStatus = OpStatus.ReadingData;
                        bytesRead = 0;
                    }
                    break;
                case OpStatus.ReadingData:
                    if (Drq)
                    {
                        LostData = true;
                    }
                    if (++bytesRead >= sectorLength)
                    {
                        crcCalc = crc;
                        opStatus = OpStatus.ReadCRCHigh;
                    }
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
                        delayTime = NMI_DELAY_IN_USEC;
                        opStatus = OpStatus.NMI;
                    }
                    else if (command.MultipleRecords)
                    {
                        SectorRegister++;
                        delayBytes = 1;
                        opStatus = OpStatus.SeekingIDAM;
                    }
                    else
                    {
                        delayTime = NMI_DELAY_IN_USEC;
                        opStatus = OpStatus.NMI;
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

                    if (command.Delay)
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
                        if (command.MarkSectorDeleted)
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
                    if (command.MultipleRecords)
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

                    if (command.Delay)
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

                    if (command.Delay)
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

                    if (command.Delay)
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
            clock.RegisterPulseReq(motorOffPulseReq, true);
        }
        private void MotorOffCallback()
        {
            MotorOn = false;
            sound.DriveMotorRunning = false;
            IntMgr.FdcMotorOffNmiLatch.Latch();
        }
    }
}
