/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

namespace Sharp80
{
    internal interface IZ80_Status
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
        ushort DepVal{ get; }
        ushort HlpVal { get; }
        ushort IrVal { get; }
        ushort WzVal { get; }
    }
}
