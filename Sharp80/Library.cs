using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Sharp80
{
	internal static partial class Lib
	{
        //private static char[] hexDigits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        public static string GetAppPath()
        {
            return Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
        }
        public static byte[] Compress(byte[] data)
        {
            var output = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(output, CompressionLevel.Optimal))
            {
                ds.Write(data, 0, data.Length);
            }
            var o = output.ToArray();
            System.Diagnostics.Debug.Assert(arrayequals(Decompress(o), data));
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
        public static bool arrayequals(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length)
                return false;
            for (int i = 0; i < b1.Length; i++)
                if (b1[i] != b2[i])
                    return false;
            return true;
        }
        //public static string ToHexString(byte[] bytes)
        //{
        //    char[] chars = new char[bytes.Length * 2];

        //    int b;

        //    for (int i = 0; i < bytes.Length; i++)
        //    {
        //        b = bytes[i];
        //        chars[i * 2] = hexDigits[b >> 4];
        //        chars[i * 2 + 1] = hexDigits[b & 0xF];
        //    }
        //    return new string(chars);
        //}

        public static string ToHexString(byte Input)
		{
            return Input.ToString("X2");

            //byte[] b = new byte[1];
            //b[0] = input;

            //return ToHexString(b);
		}
        public static string ToHexString(char Input)
        {
            return ToHexString((byte)Input);
        }

        public static string ToHexString(ushort input)
		{
            return input.ToString("X4");
            //byte[] b = new byte[2];
            //b[0] = (byte)((input & 0xFF00) >> 8);
            //b[1] = (byte)(input & 0x00FF);
            //return ToHexString(b);
        }
        public static string ToHexString(sbyte input)
        {
            return (input < 0 ? "-" : "") + ToHexString((byte)(Math.Abs(input)));
        }
        public static byte ToHexCharByte(int Input)
        {
            Input &= 0x0F;

            if (Input < 0x0A)
                Input += '0';
            else
                Input += ('A' - 10);

            return (byte)Input;
        }
        public static string ToHexString(uint input)
        {
            return input.ToString("X8");

            //byte[] b = new byte[4];
            //b[0] = (byte)((input & (uint)0xFF000000) >> 24);
            //b[1] = (byte)((input & (uint)0x00FF0000) >> 16);
            //b[2] = (byte)((input & (uint)0x0000FF00) >> 8);
            //b[3] = (byte)(input &  (uint)0x000000FF);
            //return ToHexString(b);
        }
        public static string GetSpacedHex(IMemory memory, ushort index, int count)
        {
            string s = String.Empty;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    s += " ";

                s += Lib.ToHexString(memory[index++]);
            }

            return s;
        }

        public static ushort HexToUShort(string input)
        {
 #if DEBUG
            ulong l = Convert.ToUInt64(input, 16);
			if (l > 0xFFFF)
				throw new Exception("Hex To Byte Overflow");

            ushort us;
            if (!ushort.TryParse(input, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out us))
                throw new Exception("Hex To Byte Error");

#endif
            return ushort.Parse(input, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture);
        }

		public static byte HexToByte(string input)
		{
#if DEBUG
            ulong l = Convert.ToUInt64(input, 16);
			if (l > 0xFF)
				throw new Exception("Hex To Byte Overflow");
            byte b;
            if (!byte.TryParse(input, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out b))
                throw new Exception("Hex To Byte Error");
#endif
            return byte.Parse(input, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture);
		}

        public static ushort CombineBytes(byte lowOrderByte, byte highOrderByte)
        {
            return (ushort)(lowOrderByte | (highOrderByte << 8));
        }

        public static void SplitBytes(ushort NNNN, out byte lowOrderResult, out byte highOrderResult)
        {
            lowOrderResult = (byte)(NNNN & 0xFF);
            highOrderResult = (byte)((NNNN & 0xFF00) >> 8);
        }
        public static void SplitBytes(ushort NNNN, out byte? lowOrderResult, out byte? highOrderResult)
        {
            lowOrderResult = (byte)(NNNN & 0xFF);
            highOrderResult = (byte)((NNNN & 0xFF00) >> 8);
        }

        public static sbyte TwosComp(byte input)
        {
            return TWOSCOMP[input];
        }
        public static byte TwosCompInv(sbyte input)
        {
            byte val = TWOSCOMPINVERT_APPLY_0X80_OFFSET[input + 0x80];

            System.Diagnostics.Debug.Assert(val == _TwosCompInv(input));

            return val;
        }
        private static byte _TwosCompInv(sbyte input)
        {
            for (int i = 0; i < 0x100; i++)
            {
                if (TWOSCOMP[i] == input)
                    return (byte)i;
            }
            Debug.Assert(false);
            return 0;
        }

        public static string ByteToTwosCompHexString(byte input)
        {
            if ((input & 0x80) == 0x80)
                return "-" + ToHexString((byte)(1 + ((input & 0x7F) ^ 0x7F)));
            else
                return "+" + ToHexString(input);
        }
        
        public static string FirstText(string input)
        {
            // Returns the first blob of text in a line, such as the first word.
            // Space delimited
            if (input.Contains(" "))
                return input.Substring(0, input.IndexOf(' '));
            else
                return input;
        }
        public static bool IsBitSet(byte Input, byte BitNum)
        {
            return (Input & Lib.BIT[BitNum]) != 0;
            //return ((Input & (0x01 << BitNum)) != 0);
        }
        public static ushort Crc(ushort StartingCRC, byte Data)
        {
            ushort crc = StartingCRC;
            ushort mask = (ushort)(Data << 8);
            for (int i = 0; i < 8; i++)
            {
                crc = (ushort)((crc << 1) ^ ((((crc ^ mask) & 0x8000) == 0x8000) ? 0x1021 : 0));
                mask <<= 1;
            }
            return crc;
        }
        public static string HexDump(this byte[] Array)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Array.Length; i++)
                sb.AppendLine(Lib.ToHexString((uint)i) + " " + Lib.ToHexString(Array[i]));

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
                sb.Append("0x" + Lib.ToHexString(Array[i]));
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

                sb.Append("0x" + Lib.ToHexString((ushort)val));
            }
            sb.AppendLine();
            sb.Append("};");

            return sb.ToString();
        }
    }
}
