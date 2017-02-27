/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

namespace Sharp80
{
    internal class Z80_Status
    {
        public ushort PC { get; set; }
        public ushort SP { get; set; }
        public ushort AF { get; set; }
        public ushort BC { get; set; }
        public ushort DE { get; set; }
        public ushort HL { get; set; }
        public ushort IX { get; set; }
        public ushort IY { get; set; }
        public ushort AFp { get; set; }
        public ushort BCp { get; set; }
        public ushort DEp { get; set; }
        public ushort HLp { get; set; }
        public ushort IR { get; set; }
    }
}
