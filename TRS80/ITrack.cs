/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    public interface ITrack
    {
        int DataLength { get; }
        byte ReadByte(int TrackIndex, bool? DoubleDensity);
        void WriteByte(int TrackIndex, bool DoubleDensity, byte Value);
        bool HasIdamAt(int TrackIndex, bool DoubleDensity);
    }
}
