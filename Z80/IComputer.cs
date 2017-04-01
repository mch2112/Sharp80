using System;

namespace Sharp80.Z80
{
    public interface IComputer
    {
        bool IsRunning { get; }
        void Stop(bool WaitForStop);
    }
}
