/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    public static class Lib
    {
        /// <summary>
        /// Returns up to four bytes of hex values separated by spaces and right padded
        /// </summary>

        public static ushort CombineBytes(byte lowOrderByte, byte highOrderByte) => (ushort)(lowOrderByte | (highOrderByte << 8));

        public static ushort Crc(ushort StartingCRC, params byte[] Data)
        {
            ushort crc = StartingCRC;
            foreach (var d in Data)
            {
                ushort mask = (ushort)(d << 8);
                for (int i = 0; i < 8; i++)
                {
                    crc = (ushort)((crc << 1) ^ ((((crc ^ mask) & 0x8000) == 0x8000) ? 0x1021 : 0));
                    mask <<= 1;
                }
            }
            return crc;
        }
    }
}
