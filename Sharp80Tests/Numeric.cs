using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sharp80;
namespace Sharp80Tests
{
    [TestClass]
    public class Numeric
    {
        [TestMethod]
        public void SplitBytesTest()
        {
            ushort a = 0x1234;
            a.Split(out byte low, out byte high);
            Assert.IsTrue(low == 0x34 && high == 0x12);
        }
        [TestMethod]
        public void CombineBytesTest()
        {
            byte a = 0x56;
            byte b = 0xAB;
            Assert.IsTrue(Lib.CombineBytes(a, b) == 0xAB56);
            Assert.IsTrue(Lib.CombineBytes(b, a) == 0x56AB);
        }
    }
}
