using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80;

namespace Sharp80Tests
{
    [TestClass]
    public class Strings
    {
        [TestMethod]
        public void TruncateTest() => Assert.IsTrue("Gnorts!".Truncate(3) == "Gno");
        [TestMethod]
        public void RepeatTest() => Assert.IsTrue("Gnorts!".Repeat(3) == "Gnorts!Gnorts!Gnorts!");
        [TestMethod]
        public void FirstTextTest()
        {
            Assert.IsTrue("Gnorts!".FirstText() == "Gnorts!");
            Assert.IsTrue("Gnorts Mr Alien Neil Armstrong".FirstText() == "Gnorts");
        }
    }
}
