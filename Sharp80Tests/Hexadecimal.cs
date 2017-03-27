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
            ushort a = 0xABCD;
            Assert.IsTrue(a.ToHexString() == "ABCD");
            
            a = 0x89AB;
            Assert.IsTrue(a.ToHexString() == "89AB");

            a = 0x0010;
            Assert.IsTrue(a.ToHexString() == "0010");
        }

        [TestMethod]
        public void ByteToHexStringTest()
        {
            byte a = 0xAB;
            Assert.IsTrue(a.ToHexString() == "AB");

            a = 0x9A;
            Assert.IsTrue(a.ToHexString() == "9A");

            a = 0x03;
            Assert.IsTrue(a.ToHexString() == "03");
        }
        [TestMethod]
        public void ByteToTwosCompHexStringTest()
        {
            byte a = 0x80;
            Assert.IsTrue(a.ToTwosCompHexString() == "-80", "80 -> -80");
            a = 0x7F;
            Assert.IsTrue(a.ToTwosCompHexString() == "+7F", "7F -> +7F");
            a = 0xFF;
            Assert.IsTrue(a.ToTwosCompHexString() == "-01", "FF -> -01");
            a = 0x81;
            Assert.IsTrue(a.ToTwosCompHexString() == "-7F", "81 -> -7F");
            a = 0x00;
            Assert.IsTrue(a.ToTwosCompHexString() == "+00", "00 -> +00");
        }
    }
}
