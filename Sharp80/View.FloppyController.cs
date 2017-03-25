/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80
{
    class ViewFloppyController : View
    {
        protected override ViewMode Mode => ViewMode.FloppyController;
        protected override bool ForceRedraw => Computer.IsRunning || FrameReqNum % 15 == 0;
        protected override bool CanSendKeysToEmulation => false;

        protected override byte[] GetViewBytes()
        {
            var status = Computer.FloppyControllerStatus;

            string physicalData = status.MotorOn ?
                Indent($"Physical Disk Data: Dsk {status.CurrentDriveNumber} Trk {status.PhysicalTrackNum:X2} {status.DiskAngleDegrees}") +
                Indent($"Track Data Index:   {status.TrackDataIndex:X4} [{status.ValueAtTrackDataIndex:X2}]") +
                Indent("Index Hole:         " + (status.IndexDetect ? "DETECTED" : ""))
                :
                Indent($"Physical Disk Data: Dsk {status.CurrentDriveNumber} Trk {status.PhysicalTrackNum:X2}") +
                Format() +
                Format();

            string errorData = (status.SeekError | status.LostData | status.CrcError) ?
                Indent(string.Format("Errors:{0}{1}{2}", status.SeekError ? " SEEK" : "", status.LostData ? " LOST DATA" : "", status.CrcError ? " CRC ERROR" : ""))
                :
                Format();

            return PadScreen(Encoding.ASCII.GetBytes(
                Header($"{ProductInfo.PRODUCT_NAME} Floppy Controller Status") +
                Format() +
                Indent($"Drive Number:   {status.CurrentDriveNumber}") +
                Indent($"OpStatus:       {status.OperationStatus}") +
                Indent(string.Format("State:          {0} {1}", status.Busy ? "BUSY" : "    ", status.Drq ? "DRQ" : "   ")) +
                Indent($"Command Status: {status.CommandStatus}") +
                Format() +
                Indent($"Track / Sector Register:   {status.TrackRegister:X2} / {status.SectorRegister:X2}") +
                Indent($"Command / Data Register:   {status.CommandRegister:X2} / {status.DataRegister:X2}") +
                Indent(string.Format("Side / Density Mode:       {0}  / {1}", status.SideOneSelected ? "1" : "0", status.DoubleDensitySelected ? "Double" : "Single")) +
                Format() +
                physicalData +
                errorData
                ));
        }
    }
}
