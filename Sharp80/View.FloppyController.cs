/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Text;

namespace Sharp80
{
    class ViewFloppyController : View
    {
        protected override ViewMode Mode => ViewMode.FloppyController;
        protected override bool ForceRedraw => Computer.IsRunning;
        protected override byte[] GetViewBytes()
        {
            var status = Computer.FloppyControllerStatus;

            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Floppy Controller Status") +
                Format() +
                Indent(string.Format("Drive Num:      {0}", status.DiskNum)) +
                Indent(string.Format("OpStatus:       {0}", status.OpStatus)) +
                Indent(string.Format("State:          {0} {1}", status.Busy ? "BUSY" : "    ", status.DRQ ? "DRQ" : "   ")) +
                Indent("Command Status: " + status.CommandStatus) +
                Format() +
                Indent(string.Format("Track / Sector Register:   {0:X2}  / {1:X2}", status.TrackRegister, status.SectorRegister)) +
                Indent(string.Format("Command / Data Register:   {0:X2}  / {1:X2}", status.CommandRegister, status.DataRegister)) +
                Indent(string.Format("Density Mode:              {0}", status.DoubleDensity ? "Double" : "Single")) +
                Format() +
                Indent(string.Format("Physical Disk Data: Dsk {0} Trk {1:X2} {2} ", status.DiskNum, status.PhysicalTrackNum, status.DiskAngle)) +
                Indent(string.Format("Track Data Index: {0:X4} [{1:X2}]", status.TrackDataIndex, status.ByteAtTrackDataIndex)) +
                Indent("Index Hole:       " + (status.IndexHole ? "DETECTED" : "")) +
                Indent(string.Format("Errors:{0}{1}{2}", status.SeekError ? " SEEK" : "", status.LostData ? " LOST DATA" : "", status.CrcError ? " CRC ERROR" : ""))
                ));
        }
    }
}
