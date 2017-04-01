using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80;
using Sharp80.TRS80;

namespace Sharp80Tests
{
    [TestClass]
    public class Strings
    {
        [TestMethod]
        public void TruncateTest() => Assert.IsTrue("Gnorts!".Truncate(3) == "Gno");
        [TestMethod]
        public void RepeatTest() => Assert.IsTrue("Gnorts!".Repeat(3) == "Gnorts!Gnorts!Gnorts!");
    }
}
