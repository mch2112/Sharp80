/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;

namespace Sharp80.Z80
{
    public interface IMemory : IReadOnlyList<byte>
    {
        byte this[ushort Location] { get; set; }
        ushort GetWordAt(ushort Address);
        void SetWordAt(ushort Address, ushort Value);
    }
}
