/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Runtime.InteropServices;

using Sharp80.TRS80;

namespace Sharp80
{
    public class Timer : ITimer
    {
        public double TicksPerSecond { get; private set; }
        private long ticks;
        public Timer()
        { 
            long rtTicksPerSec = 0;
            QueryPerformanceFrequency(ref rtTicksPerSec);
            TicksPerSecond = rtTicksPerSec;
        }
        public long ElapsedTicks
        {
            get
            {
                QueryPerformanceCounter(ref ticks);
                return ticks;
            }
        }
        [DllImport("kernel32.dll")]
        private static extern int QueryPerformanceFrequency(ref long x);
        [DllImport("kernel32.dll")]
        private static extern int QueryPerformanceCounter(ref long x);
    }
}
