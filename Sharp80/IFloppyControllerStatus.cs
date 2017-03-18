/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    public interface IFloppyControllerStatus
    {
        string OperationStatus { get; }
        bool Busy { get; }
        bool MotorOn { get; }
        string CommandStatus { get; }
        byte TrackRegister { get; }
        byte SectorRegister { get; }
        byte CommandRegister { get; }
        byte DataRegister { get; }
        bool SideOneSelected { get; }
        bool DoubleDensitySelected { get; }
        bool Drq { get; }
        bool SeekError { get; }
        bool LostData { get; }
        bool CrcError { get;}
        byte CurrentDriveNumber { get; }
        byte PhysicalTrackNum { get; }
        string DiskAngleDegrees { get; }
        int TrackDataIndex { get; }
        byte ValueAtTrackDataIndex { get; }
        bool IndexDetect { get; }
    }
}
