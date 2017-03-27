using System;
using System.IO;
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
        [TestMethod]
        public async Task SuPlusStartTest()
        {
            await StartWithFloppy(@"\Disks\Utilities & Operating Systems\Super Utility+ 3.2.dsk");
            await computer.KeyStroke(KeyCode.Return, false, 1000);
            Assert.IsTrue(DisposeComputer(ScreenContainsText("Zap Utilities")));
        }
    }
}
