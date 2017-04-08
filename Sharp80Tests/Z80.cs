using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80.Z80;

namespace Sharp80Tests
{
    [TestClass]
    public class Z80 : Test
    {
        [TestMethod]
        public async Task InstructionTest()
        {
            InitComputer(false);
            var z80 = new Sharp80.Z80.Z80(computer);
            foreach (var i in z80.InstructionSet)
            {
                Assert.IsTrue(i.Size.IsBetween(1, 4), $"{i} has size {i.Size}");
                Assert.IsTrue(i.OpcodeSize.IsBetween(1, i.Size), $"{i} has too large OpCode size");
                Assert.IsTrue(i.TStates.IsBetween(4, 23), $"{i} has wrong tstate count");
                Assert.IsTrue(i.TStatesAlt.IsBetween(0, 7), $"{i} has wrong extra tstate count");
                Assert.IsTrue(i.TStates * 1000 == i.Ticks, $"{i} has wrong tick count");
                Assert.IsTrue((i.TStates + i.TStatesAlt) * 1000 == i.TicksWithExtra, $"{i} has wrong extra tick count");
            }
            await DisposeComputer();
        }
        [TestMethod]
        public async Task AllOpcodesHaveInstructions()
        {
            InitComputer(false);
            var z80 = new Sharp80.Z80.Z80(computer);
            for (int i = 0; i < 0x100; i++)
                for (int j = 0; j < 0x100; j++)
                    for (int k = 0; k < 0x100; k++)
                    {
                        var inst = z80.InstructionSet.GetInstruction((byte)i, (byte)j, (byte)k);
                        Assert.IsFalse(inst is null, $"Instruction from {i:X2}{j:X2}{k:X2} is null");
                        Assert.IsTrue(inst.Size > 0 && inst.Size <= 4);
                    }
            await DisposeComputer();
        }
    }
}
