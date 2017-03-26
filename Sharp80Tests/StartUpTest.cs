using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80;

namespace Sharp80Tests
{
    [TestClass]
    public class Startup : Test
    {
        [TestMethod]
        public async Task BasicStartTest()
        {
            await StartToBasic();
            Assert.IsTrue(DisposeComputer(ScreenContainsText("READY")));
        }
        [TestMethod]
        public async Task TrsdosStartTest()
        {
            await StartToTrsdos();
            Assert.IsTrue(DisposeComputer(ScreenContainsText("TRSDOS Ready")));
        }
    }
}
