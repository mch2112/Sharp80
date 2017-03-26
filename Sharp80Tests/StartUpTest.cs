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
            try
            {
                InitComputer();
                computer.Start();
                DelayMSec(500);

                KeyPress(KeyCode.Return);
                KeyPress(KeyCode.Return);

                bool res = ScreenContainsText("READY");

                DisposeComputer();

                Assert.IsTrue(res);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
         }
    }
}
