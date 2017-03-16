/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80
{
    class ViewFloppyController : View
    {
        protected override ViewMode Mode => ViewMode.FloppyController;
        protected override bool ForceRedraw => FrameReqNum % 10 == 0;
        protected override bool CanSendKeysToEmulation => false;

        protected override byte[] GetViewBytes()
        {
            var status = Computer.FloppyControllerStatus;

            string physicalData = status.MotorOn ?
                Indent(string.Format("Physical Disk Data: Dsk {0} Trk {1:X2} {2} ", status.CurrentDriveNumber, status.PhysicalTrackNum, status.DiskAngleDegrees)) +
                Indent(string.Format("Track Data Index:   {0:X4} [{1:X2}]", status.TrackDataIndex, status.ValueAtTrackDataIndex)) +
                Indent(              "Index Hole:         " + (status.IndexDetect ? "DETECTED" : ""))
                :
                Indent(string.Format("Physical Disk Data: Dsk {0} Trk {1:X2}", status.CurrentDriveNumber, status.PhysicalTrackNum)) +
                Format() +
                Format();

            string errorData = (status.SeekError | status.LostData | status.CrcError) ?
                Indent(string.Format("Errors:{0}{1}{2}", status.SeekError ? " SEEK" : "", status.LostData ? " LOST DATA" : "", status.CrcError ? " CRC ERROR" : ""))
                :
                Format();

            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 Floppy Controller Status") +
                Format() +
                Indent(string.Format("Drive Number:   {0}", status.CurrentDriveNumber)) +
                Indent(string.Format("OpStatus:       {0}", status.OperationStatus)) +
                Indent(string.Format("State:          {0} {1}", status.Busy ? "BUSY" : "    ", status.DrqStatus ? "DRQ" : "   ")) +
                Indent("Command Status: " + status.CommandStatus) +
                Format() +
                Indent(string.Format("Track / Sector Register:   {0:X2} / {1:X2}", status.TrackRegister, status.SectorRegister)) +
                Indent(string.Format("Command / Data Register:   {0:X2} / {1:X2}", status.CommandRegister, status.DataRegister)) +
                Indent(string.Format("Density Mode:              {0}", status.DoubleDensitySelected ? "Double" : "Single")) +
                Format() +
                physicalData +
                errorData
                ));
        }
    }
}
