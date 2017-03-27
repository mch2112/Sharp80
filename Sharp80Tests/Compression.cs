using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80;

namespace Sharp80Tests
{
    [TestClass]
    public class Compression
    {
        [TestMethod]
        public void CompressionTest()
        {
            string s = "Now is the time on Sprockets when we dance.";
            byte[] b = Encoding.ASCII.GetBytes(s);

            Assert.IsTrue(Encoding.ASCII.GetString(b.Compress().Decompress()) == s);
        }
    }
}
