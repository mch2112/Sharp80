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
            ((ushort)0x1234).Split(out byte low, out byte high);
            Assert.IsTrue(low == 0x34 && high == 0x12);
        }
        [TestMethod]
        public void CombineBytesTest()
        {
            Assert.IsTrue(Lib.CombineBytes(0x56, 0xAB) == 0xAB56);
            Assert.IsTrue(Lib.CombineBytes(0xAB, 0x56) == 0x56AB);
        }
    }
}
