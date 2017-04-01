using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80.TRS80;

namespace Sharp80Tests
{
    [TestClass]
    public class Startup : Test
    {
        [TestMethod]
        public async Task BasicStartTest()
        {
            await StartToBasic();
            bool result = ScreenContainsText("READY");
            await DisposeComputer();
            Assert.IsTrue(result);
        }
        [TestMethod]
        public async Task TrsdosStartTest()
        {
            await StartToTrsdos();
            bool result = ScreenContainsText("TRSDOS Ready");
            await DisposeComputer();
            Assert.IsTrue(result);
        }
        [TestMethod]
        public async Task SuPlusStartTest()
        {
            await StartWithFloppy(@"\Disks\Utilities & Operating Systems\Super Utility+ 3.2.dsk");
            await computer.KeyStroke(KeyCode.Return, false, 1000);
            bool result = ScreenContainsText("Zap Utilities");
            await DisposeComputer();
            Assert.IsTrue(result);
        }
    }
}
