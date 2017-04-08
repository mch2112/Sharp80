using System;

namespace Sharp80.Z80
{
    public interface IComputer
    {
        IMemory Memory {get; }
        IPorts Ports { get; }
        bool IsRunning { get; }
        ulong ElapsedTStates { get; }
        void Stop(bool WaitForStop);
    }
}
