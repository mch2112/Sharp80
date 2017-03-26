using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
