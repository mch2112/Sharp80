using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80;

namespace Sharp80Tests
{
    [TestClass]
    public class Startup : Test
    {
        [TestMethod]
        public void StartToBasic()
        {
            InitComputer();
            computer.Start();
            DelayMSec(500);
            KeyPress(KeyCode.Return);
            KeyPress(KeyCode.Return);
            Assert.IsTrue(ScreenContainsText("READY"));
            DisposeComputer();
        }
    }
}
