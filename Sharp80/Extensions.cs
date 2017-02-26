using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    public static class Extensions
    {

        private static readonly byte[] BIT = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        //private static readonly byte[] NOT = { 0xFE, 0xFD, 0xFB, 0xF7, 0xEF, 0xDF, 0xBF, 0x7F };

        /// <summary>
        /// Get the subarray, inclusive for start index, exclusive for end index.
        /// Negative End means take the rest of the array
        /// </summary>
        public static T[] Slice<T>(this T[] Source, int Start, int End = -1)
        {
            if (End < 0)
                End = Source.Length;
            else
                End = Math.Min(End, Source.Length);

            if (Start == 0 && End == Source.Length)
                return Source;

            var length = End - Start;

            var ret = new T[length];
            for (int i = 0; i < length; i++)
                ret[i] = Source[i + Start];

            return ret;
        }
        public static T[] Concat<T>(this T[] Array1, T[] Array2)
        {
            var ret = new T[Array1.Length + Array2.Length];
            Array.Copy(Array1, ret, Array1.Length);
            Array.Copy(Array2, 0, ret, Array1.Length, Array2.Length);
            return ret;
        }
        public static ushort[] ToUShortArray(this byte[] Source)
        {
            if (Source.Length % 2 > 0)
                throw new Exception("ToUShortArray: Requires even length source.");

            ushort[] ret = new ushort[Source.Length / 2];
            int j = 0;
            for (int i = 0; i < Source.Length; i += 2, j++)
            {
                ret[j] = (ushort)((Source[i + 1] << 8) | (Source[i]));
            }
            return ret;
        }
        public static byte[] ToByteArray(this ushort[] Source)
        {
            byte[] ret = new byte[Source.Length * 2];
            int j = 0;
            for (int i = 0; i < Source.Length; i++, j += 2)
                Lib.SplitBytes(Source[i], out ret[j], out ret[j + 1]);
            return ret;
        }
        public static T[] Double<T>(this T[] Source)
        {
            var ret = new T[Source.Length * 2];
            for (int i = 0; i < Source.Length; i++)
                ret[i * 2] = ret[i * 2 + 1] = Source[i];
            return ret;
        }
        public static T[] Truncate<T>(this T[] Source, int MaxLength)
        {
            if (Source.Length <= MaxLength)
                return Source;

            return Source.Slice(0, MaxLength);
        }
        public static void SetAll<T>(this T[] Array, T Value)
        {
            for (int i = 0; i < Array.Length; i++)
                Array[i] = Value;
        }
        public static void SetValues<T>(this T[] Array, ref int Start, int Length, T Value)
        {
            int end = Start + Length;
            for (; Start < end; Start++)
                Array[Start] = Value;
        }
        public static void SetValues<T>(this T[] Array, ref int Start, bool Double, params T[] Values)
        {
            foreach (T v in Values)
            {
                Array[Start++] = v;
                if (Double)
                    Array[Start++] = v;
            }
        }

        /// <summary>
        /// Pads an array to minimum length with given value
        /// NOTE: The origiinal array might be returned!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Array"></param>
        /// <param name="Length">The minimum desired length</param>
        /// <param name="Value">The value to pad with</param>
        /// <returns></returns>
        public static T[] Pad<T>(this T[] Array, int Length, T Value)
        {
            if (Array.Length >= Length)
                return Array;

            var ret = Array.Concat(new T[Length - Array.Length]);

            for (int i = Array.Length; i < Length; i++)
                ret[i] = Value;

            return ret;
        }
        public static bool ArrayEquals<T>(this T[] Source, T[] Other)
        {
            return Source.SequenceEqual(Other);

            //if (Source.Length != Other.Length)
            //    return false;
            //for (int i = 0; i < Source.Length; i++)
            //    if (!EqualityComparer<T>.Default.Equals(Source[i], Other[i]))
            //        return false;
            //return true;
        }
        public static bool IsBitSet(this byte Input, byte BitNum)
        {
            return (Input & BIT[BitNum]) != 0;
        }
        public static bool RotateAddress(this ushort Input, char c, out ushort Output)
        {
            if (c != '\0')
            {
                string str = Lib.ToHexString(Input) + c;
                if (str.Length > 4)
                    str = str.Substring(str.Length - 4, 4);

                return ushort.TryParse(str,
                                    System.Globalization.NumberStyles.AllowHexSpecifier,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out Output);
            }
            else
            {
                Output = 0;
                return false;
            }
        }
    }
}
