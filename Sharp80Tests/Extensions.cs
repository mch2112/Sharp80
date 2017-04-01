using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80Tests
{
    internal static class Extensions
    {
        public static bool Contains(this IReadOnlyList<byte> Bytes, byte[] Pattern)
        {
            var bytes = Bytes.ToArray();

            int patternLength = Pattern.Length;
            int totalLength = bytes.Length;
            byte firstMatchByte = Pattern[0];
            for (int i = 0; i < totalLength; i++)
            {
                if (firstMatchByte == bytes[i] && totalLength - i >= patternLength)
                {
                    byte[] match = new byte[patternLength];
                    Array.Copy(bytes, i, match, 0, patternLength);
                    if (match.SequenceEqual<byte>(Pattern))
                        return true;
                }
            }
            return false;
        }

        public static byte[] ToByteArray(this string Text)
        {
            return Encoding.ASCII.GetBytes(Text);
        }
        /*
        public static string ToHexString(this byte Input) => Input.ToString("X2");
        public static string ToHexString(this ushort Input) => Input.ToString("X4");
        public static string ToTwosCompHexString(this byte input)
        {
            if ((input & 0x80) == 0x80)
                return "-" + ((byte)(1 + ((input & 0x7F) ^ 0x7F))).ToHexString();
            else
                return "+" + input.ToHexString();
        }
    */
    }
}
