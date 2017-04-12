using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80.TRS80;

namespace Sharp80Tests
{
    [TestClass]
    public class Tapes : Test
    {
        [TestMethod]
        public async Task TapeReadTest()
        {
            await TapeReadTestCore(true);
        }
        [TestMethod]
        public async Task TapeReadTestAlt()
        {
            await TapeReadTestCore(false);
        }
        public async Task TapeReadTestCore(bool PlayBeforeTyping)
        {
            await StartToBasic();
            computer.TapeLoad(Path.Combine(Path.Combine(Storage.AppDataPath, "Tapes"), "Magic Carpet (SYSTEM IF) 1500 Baud.cas"));
            Assert.IsTrue(computer.TapeLength > 5000, "Tape not loaded");
            if (PlayBeforeTyping)
                computer.TapePlay();
            await PasteLine("SYSTEM");
            await computer.Delay(500);
            await PasteLine("IF");
            if (!PlayBeforeTyping)
                computer.TapePlay();
            await computer.Delay(10000);
            Assert.IsTrue(computer.TapeCounter > 1600, $"Tape not running, counter: {computer.TapeCounter:0000.0}");
            await PasteLine("/");
            await computer.Delay(10000);
            Assert.IsTrue(ScreenContainsText("INTERACTIVE FICTION"), "Failed looking for 'INTERACTIVE FICTION', found: " + Environment.NewLine + computer.ScreenText);
            await computer.Delay(90000);
            Assert.IsTrue(ScreenContainsText("THE MAGIC CARPET"), "Failed looking for 'THE MAGIC CARPET'");
            await DisposeComputer();
        }
        [TestMethod]
        public async Task TapeLoadLowSpeedReadTest()
        {
            await StartToBasic(ClockSpeed.Unlimited, false);
            computer.TapeLoad(Path.Combine(Path.Combine(Storage.AppDataPath, "Tapes"), "Eliza (SYSTEM E) 500 Baud.cas"));
            Assert.IsTrue(computer.TapeLength > 5000, "Tape not loaded");
            computer.TapePlay();
            await PasteLine("SYSTEM");
            await computer.Delay(500);
            await PasteLine("E");
            await computer.Delay(200000);
            Assert.IsTrue(computer.TapeCounter > 1600, $"Tape not running, counter: {computer.TapeCounter:0000.0}");
            await PasteLine("/");
            await computer.Delay(5000);
            Assert.IsTrue(ScreenContainsText("PLEASE STATE YOUR PROBLEM"), "Failed looking for 'PLEASE STATE YOUR PROBLEM', found: " + Environment.NewLine + computer.ScreenText);
            await DisposeComputer();
        }
        [TestMethod]
        public async Task TapeWriteTest()
        {
            await StartToBasic();
            await PasteLine("10 X = X + 1");
            await PasteLine("20 PRINT X");
            await PasteLine("30 GOTO 10");
            computer.TapeRecord();
            await PasteLine("CSAVE \"FOO\"");
            await computer.Delay(10000);
            await PasteLine("CLS");
            await PasteLine("NEW");
            await PasteLine("LIST");
            Assert.IsFalse(ScreenContainsText("PRINT X"));
            computer.TapeRewind();
            computer.TapePlay();
            await PasteLine("CLOAD");
            await computer.Delay(10000);
            await PasteLine("CLS");
            await PasteLine("LIST");
            Assert.IsTrue(ScreenContainsText("PRINT X"));
            await DisposeComputer();
        }
        [TestMethod]
        public async Task TapeLowSpeedWriteTest()
        {
            await StartToBasic(ClockSpeed.Unlimited, false);
            await PasteLine("10 Y = Y + 3");
            await PasteLine("20 PRINT Y");
            await PasteLine("30 GOTO 10");
            computer.TapeRecord();
            await PasteLine("CSAVE \"BAR\"");
            await computer.Delay(10000);
            await PasteLine("CLS");
            await PasteLine("NEW");
            await PasteLine("LIST");
            Assert.IsFalse(ScreenContainsText("PRINT Y"));
            computer.TapeRewind();
            computer.TapePlay();
            await PasteLine("CLOAD");
            await computer.Delay(10000);
            await PasteLine("CLS");
            await PasteLine("LIST");
            Assert.IsTrue(ScreenContainsText("PRINT Y"));
            await DisposeComputer();
        }
    }
}
