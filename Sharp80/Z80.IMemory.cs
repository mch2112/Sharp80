using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.Processor
{
    internal interface IMemory : IReadOnlyList<byte>
    {
        byte this[ushort Location] { get; set; }
        ushort GetWordAt(ushort Address);
        void SetWordAt(ushort Address, ushort Value);
    }
}
