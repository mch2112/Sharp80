/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.Z80
{
    public interface IStatus
    {
        ushort PcVal { get; }
        ushort SpVal { get; }
        ushort AfVal { get; }
        ushort BcVal { get; }
        ushort DeVal { get; }
        ushort HlVal { get; }
        ushort IxVal { get; }
        ushort IyVal { get; }
        ushort AfpVal { get; }
        ushort BcpVal { get; }
        ushort DepVal { get; }
        ushort HlpVal { get; }
        ushort IrVal { get; }
        ushort WzVal { get; }
    }
}
