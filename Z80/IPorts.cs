using System;

namespace Sharp80.Z80
{
    public interface IPorts
    {
        byte this[byte PortNumber] { get; set; }
    }
}
