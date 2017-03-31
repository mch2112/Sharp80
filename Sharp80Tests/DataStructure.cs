using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sharp80;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sharp80Tests
{
    [TestClass]
    public class DataStructure : Test
    {
        [TestMethod]
        public void CircularBufferTest()
        {
            var r = new Random();

            int num = r.Next(1000, 10000);
            int size = r.Next(10, 100);
            
            var cb = new Sharp80.Processor.CircularBuffer(size);
            for (ushort i = 0; i <= num; i++)
                cb.Add(i);

            bool ok = true;
            var j = num - size;

            foreach (var us in cb)
                ok &= us == ++j;

            Assert.IsTrue(ok, $"CircularBuffer test failed with size {size} and {num} added items.");
        }
    }
}
