/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.Z80
{
    public interface IPorts
    {
        byte this[byte PortNumber] { get; set; }
    }
}
