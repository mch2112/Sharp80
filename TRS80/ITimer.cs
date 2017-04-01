/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    public interface ITimer
    {
        double TicksPerSecond { get; }
        long ElapsedTicks { get; }
    }
}
