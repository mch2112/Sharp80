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
        // TODO: Low Baud tests

        [TestMethod]
        public async Task TapeLoadTest()
        {
            await StartToBasic();
            computer.TapeLoad(Path.Combine(Path.Combine(Storage.AppDataPath, @"Tapes" + Path.DirectorySeparatorChar), "Magic Carpet (SYSTEM IF) 1500 Baud.cas"));
            Assert.IsTrue(computer.TapeLength > 5000, "Tape not loaded");
            computer.TapePlay();
            await PasteLine("SYSTEM");
            await computer.Delay(500);
            await PasteLine("IF");
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
        public async Task TapeRecordTest()
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
    }
}
