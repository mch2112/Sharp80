/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sharp80
{
	internal static partial class Lib
	{
        public static byte[] Compress(byte[] data)
        {
            var output = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(output, CompressionLevel.Optimal))
            {
                ds.Write(data, 0, data.Length);
            }
            var o = output.ToArray();
            Debug.Assert(Decompress(o).ArrayEquals(data));
            return o;
        }
        public static byte[] Decompress(byte[] data)
        {
            var input = new MemoryStream(data);
            var output = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(input, CompressionMode.Decompress))
            {
                ds.CopyTo(output);
            }
            var o = output.ToArray();
            return o;
        }
        public static string GetSpacedHex(IMemory memory, ushort index, int count)
        {
            string s = String.Empty;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    s += " ";
                s += memory[index++].ToHexString();
            }

            return s;
        }

        public static ushort HexToUShort(string input)
        {
            return ushort.Parse(input,
                                System.Globalization.NumberStyles.AllowHexSpecifier,
                                System.Globalization.CultureInfo.InvariantCulture);
        }

		public static byte HexToByte(string input)
		{
            return byte.Parse(input,
                              System.Globalization.NumberStyles.AllowHexSpecifier,
                              System.Globalization.CultureInfo.InvariantCulture);
		}

        public static ushort CombineBytes(byte lowOrderByte, byte highOrderByte)
        {
            return (ushort)(lowOrderByte | (highOrderByte << 8));
        }
        public static byte TwosCompInv(sbyte input)
        {
            byte val = TWOSCOMPINVERT_APPLY_0X80_OFFSET[input + 0x80];
            return val;
        }

        /// <summary>
        /// Returns the first blob of text in a line, such as the first word. Space delimited.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string FirstText(string input)
        {
            if (input.Contains(" "))
                return input.Substring(0, input.IndexOf(' '));
            else
                return input;
        }
        
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
        public static string HexDump(this byte[] Array)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Array.Length; i++)
                sb.AppendLine(((uint)i).ToHexString() + " " + Array[i].ToHexString());

            return sb.ToString();
        }
        public static string ToByteArrayDefinition(this byte[] Array)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("new byte[]");
            sb.AppendLine("{");
            sb.Append("\t");

            for (int i = 0; i < Array.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                    if (i % 0x10 == 0x00)
                    {
                        sb.AppendLine();
                        sb.Append("\t");
                    }
                }
                sb.Append("0x" + Array[i].ToHexString());
            }
            sb.AppendLine();
            sb.Append("};");

            return sb.ToString();
        }
        public static string ToShortArrayDefinition(this byte[] Array)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("new short[]");
            sb.AppendLine("{");
            sb.Append("\t");
            
            for (int i = 0; i < Array.Length - 1; i += 2)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                    if (i % 0x20 == 0x00)
                    {
                        sb.AppendLine();
                        sb.Append("\t");
                    }
                }
                
                int val = ((Array[i + 1] << 8) + Array[i]);

                bool negative = val > 0x7FFF;

                if (negative)
                {
                    sb.Append("-");
                    val = 0x10000 - val;
                }
                else
                    sb.Append(" ");

                sb.Append("0x" + ((ushort)val).ToHexString());
            }
            sb.AppendLine();
            sb.Append("};");

            return sb.ToString();
        }
    }
}
