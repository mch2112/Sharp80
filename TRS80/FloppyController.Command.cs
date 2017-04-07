using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    public partial class FloppyController : ISerializable, IFloppyControllerStatus
    {
        private enum FdcCommandType { Reset, Restore, Seek, Step, ReadSector, WriteSector, ReadTrack, WriteTrack, ReadAddress, ForceInterrupt, ForceInterruptImmediate, Invalid }
        private struct FdcCommand
        {
            public byte CommandRegister { get;  }
            public FdcCommandType Type { get; }

            private static ulong[] stepRates;

            // CONSTRUCTORS

            static FdcCommand()
            {
                stepRates = new ulong[4]
                {
                     6 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ,
                    12 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ,
                    20 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ,
                    30 * MILLISECONDS_TO_MICROSECONDS / FDC_CLOCK_MHZ
                };
            }
            public FdcCommand(byte CommandRegister)
            {
                this.CommandRegister = CommandRegister;
                switch (CommandRegister & 0xF0)
                {
                    case 0x00:
                        Type = FdcCommandType.Restore;
                        break;
                    case 0x10:
                        Type = FdcCommandType.Seek;
                        break;
                    case 0x20:
                    case 0x30:
                    case 0x40:
                    case 0x50:
                    case 0x60:
                    case 0x70:
                        Type = FdcCommandType.Step;
                        break;
                    case 0x80:
                    case 0x90:
                        Type = FdcCommandType.ReadSector;
                        break;
                    case 0xA0:
                    case 0xB0:
                        Type = FdcCommandType.WriteSector;
                        break;
                    case 0xC0: // read address
                        Type = FdcCommandType.ReadAddress;
                        break;
                    case 0xD0:
                        if (CommandRegister == 0xD0)
                            Type = FdcCommandType.Reset;
                        else if (CommandRegister == 0xD8)
                            Type = FdcCommandType.ForceInterruptImmediate;
                        else
                            Type = FdcCommandType.ForceInterrupt;
                        break;
                    case 0xE0: // read track
                        Type = FdcCommandType.ReadTrack;
                        break;
                    case 0xF0:  // write track
                        Type = FdcCommandType.WriteTrack;
                        break;
                    default:
                        Type = FdcCommandType.Invalid;
                        break;
                }
            }
            
            public int CommandCategory
            {
                get
                {
                    switch (Type)
                    {
                        case FdcCommandType.Restore:
                        case FdcCommandType.Seek:
                        case FdcCommandType.Step:
                            return 1;
                        case FdcCommandType.ReadSector:
                        case FdcCommandType.WriteSector:
                            return 2;
                        case FdcCommandType.ReadTrack:
                        case FdcCommandType.WriteTrack:
                        case FdcCommandType.ReadAddress:
                            return 3;
                        case FdcCommandType.ForceInterrupt:
                        case FdcCommandType.ForceInterruptImmediate:
                            return 4;
                        default:
                            return 0;
                    }
                }
            }

            public ulong StepRate => stepRates[CommandRegister & 0x03];
            public bool MarkSectorDeleted => CommandRegister.IsBitSet(0);
            public bool SideSelectVerify => CommandRegister.IsBitSet(1);
            public bool TypeOneVerify => CommandRegister.IsBitSet(2);
            public bool Delay => CommandRegister.IsBitSet(2);
            public bool SideOneExpected => CommandRegister.IsBitSet(3);
            public bool MultipleRecords => CommandRegister.IsBitSet(4);
            public bool UpdateTrackRegister => CommandRegister.IsBitSet(4);

            public byte GetStatus(bool MotorOn, bool Busy, bool IndexDetect, bool DriveLoaded, bool WriteProtected, bool Drq, bool SectorDeleted, bool SeekError, bool CrcError, bool LostData, bool OnTrackZero)
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

                byte statusRegister = 0x00;
                bool indexHole = false;
                bool headLoaded = false;
                if (!MotorOn)
                {
                    statusRegister |= 0x80; // not ready
                }
                else if (DriveLoaded)
                {
                    indexHole = IndexDetect;
                    headLoaded = true;
                }

                switch (Type)
                {
                    case FdcCommandType.Restore:
                    case FdcCommandType.Seek:
                    case FdcCommandType.Step:
                    case FdcCommandType.Reset:
                        if (WriteProtected)
                            statusRegister |= 0x40;   // Bit 6: Write Protect detect
                        if (headLoaded)
                            statusRegister |= 0x20;   // Bit 5: head loaded and engaged
                        if (SeekError)
                            statusRegister |= 0x10;   // Bit 4: Seek error
                        if (CrcError)
                            statusRegister |= 0x08;   // Bit 3: CRC Error
                        if (OnTrackZero) // Bit 2: Track Zero detect
                            statusRegister |= 0x04;
                        if (indexHole)
                            statusRegister |= 0x02;   // Bit 1: Index Detect
                        break;
                    case FdcCommandType.ReadAddress:
                        if (SeekError)
                            statusRegister |= 0x10; // Bit 4: Record Not found
                        if (CrcError)
                            statusRegister |= 0x08; // Bit 3: CRC Error
                        if (LostData)
                            statusRegister |= 0x04; // Bit 2: Lost Data
                        if (Drq)
                            statusRegister |= 0x02; // Bit 1: DRQ
                        break;
                    case FdcCommandType.ReadSector:
                        if (SectorDeleted)
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
                    case FdcCommandType.ReadTrack:
                        if (LostData)
                            statusRegister |= 0x04; // Bit 2: Lost Data
                        if (Drq)
                            statusRegister |= 0x02; // Bit 1: DRQ
                        break;
                    case FdcCommandType.WriteSector:
                        if (WriteProtected)
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
                    case FdcCommandType.WriteTrack:
                        if (WriteProtected)
                            statusRegister |= 0x40; // Bit 6: Write Protect detect
                        if (LostData)
                            statusRegister |= 0x04; // Bit 2: Lost Data
                        if (Drq)
                            statusRegister |= 0x02; // Bit 1: DRQ
                        break;
                }
                if (Busy)
                    statusRegister |= 0x01; // Bit 0: Busy

                return statusRegister;
            }

            public override string ToString() => Type.ToString();
        }
    }
}
