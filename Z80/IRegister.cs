/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.Z80
{
    internal interface IRegister<T> where T : struct
    {
        string Name { get; }
        T val { get; set; }
        void inc();
        void dec();
        bool NZ { get; }
    }
    internal interface IRegisterCompound : IRegister<ushort>
    {
        IRegister<byte> H { get; }
        IRegister<byte> L { get; }
    }
    internal interface IRegisterIndexed : IRegister<byte>
    {
        ushort OffsetAddress { get; }
        IRegister<ushort> Proxy { get; }
    }
}
