using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sharp80;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sharp80Tests
{
    [TestClass]
    public class Hexadecimal
    {
        [TestMethod]
        public void UShortToHexStringTest()
        {
            Assert.IsTrue(((ushort)0xABCD).ToHexString() == "ABCD");
            Assert.IsTrue(((ushort)0x89AB).ToHexString() == "89AB");
            Assert.IsTrue(((ushort)0x0010).ToHexString() == "0010");
        }

        [TestMethod]
        public void ByteToHexStringTest()
        {
            Assert.IsTrue(((byte)0xAB).ToHexString() == "AB");
            Assert.IsTrue(((byte)0x9A).ToHexString() == "9A");
            Assert.IsTrue(((byte)0x03).ToHexString() == "03");
        }
        [TestMethod]
        public void ByteToTwosCompHexStringTest()
        {
            Assert.IsTrue(((byte)0x80).ToTwosCompHexString() == "-80", "80 -> -80");
            Assert.IsTrue(((byte)0x7F).ToTwosCompHexString() == "+7F", "7F -> +7F");
            Assert.IsTrue(((byte)0xFF).ToTwosCompHexString() == "-01", "FF -> -01");
            Assert.IsTrue(((byte)0x81).ToTwosCompHexString() == "-7F", "81 -> -7F");
            Assert.IsTrue(((byte)0x00).ToTwosCompHexString() == "+00", "00 -> +00");
        }
    }
}
