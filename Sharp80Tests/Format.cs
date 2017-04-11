using System;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80.TRS80;

namespace Sharp80Tests
{
    [TestClass]
    public class Format : Test
    {
        [TestMethod]
        public async Task FormatDiskTestLdosDD()
        {
            await StartWithFloppy(@"\Disks\Utilities & Operating Systems\LDOS 5.31 - Model III.dsk", ClockSpeed.Unlimited);

            await PasteLine("11/11/99");
            await PasteLine();
            await computer.Delay(5000);

            computer.LoadFloppy(1, new Floppy(false));

            Assert.IsTrue(ScreenContainsText("LDOS Ready"), "LDOS ready 1 msg not found.");
            await PasteLine("FORMAT :1");
            await computer.Delay(7000);

            Assert.IsTrue(ScreenContainsText("Diskette name ?"), "Diskette msg not found.");
            await PasteLine("FOO");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Master password ?"), "Password msg not found.");
            await PasteLine();
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Single or Double density <S,D> ?"), "Density msg not found.");
            await PasteLine("D");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Enter number of sides <1,2> ?"), "Num sides msg not found.");
            await PasteLine("2");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Number of cylinders ?"), "Num cylinders msg not found.");
            await PasteLine("40");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Boot strap stepping rate <6, 12, 20, 30 msecs> ?"), "Boot strap msg not found.");
            await PasteLine("20");
            await computer.Delay(100000);

            Assert.IsTrue(ScreenContainsText("Formatting complete"), "Formatting complete msg not found.");
            Assert.IsTrue(ScreenContainsText("LDOS Ready"), "LDOS ready 2 msg not found.");

            await DisposeComputer();
        }
        [TestMethod]
        public async Task FormatDiskTestLdosSD()
        {
            await StartWithFloppy(@"\Disks\Utilities & Operating Systems\LDOS 5.31 - Model III.dsk", ClockSpeed.Unlimited);

            await PasteLine("11/11/99");
            await PasteLine();
            await computer.Delay(5000);

            computer.LoadFloppy(1, new Floppy(false));

            Assert.IsTrue(ScreenContainsText("LDOS Ready"), "LDOS ready 1 msg not found.");
            await PasteLine("FORMAT :1");
            await computer.Delay(7000);

            Assert.IsTrue(ScreenContainsText("Diskette name ?"), "Diskette msg not found.");
            await PasteLine("FOO");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Master password ?"), "Password msg not found.");
            await PasteLine();
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Single or Double density <S,D> ?"), "Density msg not found.");
            await PasteLine("S");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Enter number of sides <1,2> ?"), "Num sides msg not found.");
            await PasteLine("1");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Number of cylinders ?"), "Num cylinders msg not found.");
            await PasteLine("35");
            await computer.Delay(1000);

            Assert.IsTrue(ScreenContainsText("Boot strap stepping rate <6, 12, 20, 30 msecs> ?"), "Boot strap msg not found.");
            await PasteLine("12");
            await computer.Delay(100000);

            Assert.IsTrue(ScreenContainsText("Formatting complete"), "Formatting complete msg not found.");
            Assert.IsTrue(ScreenContainsText("LDOS Ready"), "LDOS ready 2 msg not found.");

            await DisposeComputer();
        }

    }
}
