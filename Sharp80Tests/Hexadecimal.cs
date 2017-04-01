using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sharp80;
using Sharp80.TRS80;
using Sharp80.Z80;

namespace Sharp80Tests
{
    [TestClass]
    public class Hexadecimal
    {
        [TestMethod]
        public void UShortToHexStringTest()
        {
            Assert.IsTrue(Sharp80.Z80.Extensions.ToHexString((ushort)0xABCD) == "ABCD");
            Assert.IsTrue(Sharp80.Z80.Extensions.ToHexString((ushort)0x89AB) == "89AB");
            Assert.IsTrue(Sharp80.Z80.Extensions.ToHexString((ushort)0x0010) == "0010");
        }
        [TestMethod]
        public void ByteToHexStringTest()
        {
            Assert.IsTrue(Sharp80.Z80.Extensions.ToHexString((byte)0xAB) == "AB");
            Assert.IsTrue(Sharp80.Z80.Extensions.ToHexString((byte)0x9A) == "9A");
            Assert.IsTrue(Sharp80.Z80.Extensions.ToHexString((byte)0x03) == "03");
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
