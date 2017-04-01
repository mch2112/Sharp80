using System;
using System.Threading.Tasks;
using Sharp80.TRS80;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sharp80Tests
{
    [TestClass]
    public class ProgramFlow : Test
    {
        [TestMethod]
        public async Task BreakpointTest()
        {
            InitComputer(false, false);
            computer.NormalSpeed = false;
            computer.BreakPoint = 0x1A7B; // BASIC Ready
            computer.BreakPointOn = true;
            await computer.StartAndAwait();
            await computer.Delay(1000);
            await KeyPress(KeyCode.Return, false, 500);
            await KeyPress(KeyCode.Return, false, 500);
            await computer.Delay(2000);
            Assert.IsTrue(computer.ProgramCounter == 0x1A7B, $"PC not at expected 1A7B, instead {computer.ProgramCounter:X4}");
            computer.Dispose();
        }
    }
}
