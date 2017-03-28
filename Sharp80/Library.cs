/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;

namespace Sharp80
{
    public static partial class Lib
    {
        /// <summary>
        /// Returns up to four bytes of hex values separated by spaces and right padded
        /// </summary>
        public static string GetSpacedHex(IReadOnlyList<byte> memory, ushort index, int count)
        {
            switch (count)
            {
                case 0:
                    return "           ";
                case 1:
                    return $"{memory[index]:X2}         ";
                case 2:
                    return $"{memory[index]:X2} {memory[index + 1]:X2}      ";
                case 3:
                    return $"{memory[index]:X2} {memory[index + 1]:X2} {memory[index + 2]:X2}   ";
                case 4:
                    return $"{memory[index]:X2} {memory[index + 1]:X2} {memory[index + 2]:X2} {memory[index + 3]:X2}";
                default:
                    throw new Exception();
            }
        }
        public static ushort HexToUShort(string input) => ushort.Parse(input,
                                                                       System.Globalization.NumberStyles.AllowHexSpecifier,
                                                                       System.Globalization.CultureInfo.InvariantCulture);

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
