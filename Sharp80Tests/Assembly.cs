using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sharp80;
using Sharp80.Assembler;

namespace Sharp80Tests
{
    [TestClass]
    public class Assembly : Test
    {
        private string NL = Environment.NewLine;

        [TestMethod]
        public async Task AssemblerTest()
        {
            await DoAssembly();

            Log($"TS Before: {computer.GetElapsedTStates():0,000,000}");
            Log($"PC Before: {computer.ProgramCounter:X4}");

            await computer.StartAndAwait();
            await computer.Delay(200);

            Log($"TS After: {computer.GetElapsedTStates():0,000,000}");
            Log($"PC After: {computer.ProgramCounter:X4}");
            Log($"IsRunning: {computer.IsRunning}");
            Log($"IsStopped: {computer.IsStopped}");

            // 12345 * 34567 = 0x196F608F

            Assert.IsTrue(computer.ProgramCounter == 0x8029, $"PC ({computer.ProgramCounter:X4}H) not on HALT instruction");
            Assert.IsTrue(computer.Memory[0x9000] == 0x8F, $"(9000H) should be 8F, is {computer.Memory[0x9000]}");
            Assert.IsTrue(computer.Memory[0x9001] == 0x60, $"(9001H) should be 60, is {computer.Memory[0x9001]}");
            Assert.IsTrue(computer.Memory[0x9002] == 0x6F, $"(9002H) should be 6F, is {computer.Memory[0x9002]}");
            Assert.IsTrue(computer.Memory[0x9003] == 0x19, $"(9003H) should be 19, is {computer.Memory[0x9003]}");

            await DisposeComputer();
        }
        [TestMethod]
        public async Task DisassemblerTest()
        {
            await DoAssembly();
            var dis = computer.Disassemble(0x8000, 0x8029, true);

            // remove comment lines
            dis = string.Join(Environment.NewLine, dis.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(l => !l.Trim().StartsWith(";")));

            Assert.IsTrue(dis == "\tDI" + NL + 
                                 "\tLD\tHL, 0000H" + NL +
                                 "\tLD\tA, 10H" + NL +
                                 "\tADD\tHL, HL" + NL +
                                 "\tRL\tE" + NL +
                                 "\tRL\tD" + NL +
                                 "\tJP\tNC, 8013H" + NL +
                                 "\tADD\tHL, BC" + NL +
                                 "\tJP\tNC, 8013H" + NL +
                                 "\tINC\tDE" + NL +
                                 "\tDEC\tA" + NL +
                                 "\tJP\tNZ, 8006H" + NL +
                                 "\tRET" + NL +
                                 "\tLD\tBC, 3039H" + NL +
                                 "\tLD\tDE, 8707H" + NL +
                                 "\tCALL\t8000H" + NL +
                                 "\tLD\t(9000H), HL" + NL +
                                 "\tPUSH\tDE" + NL +
                                 "\tPOP\tHL" + NL +
                                 "\tLD\t(9002H), HL" + NL +
                                 "\tHALT");
        }

        private async Task DoAssembly()
        {
            // Simple multibyte multiplication program
            string program =
                ";Mul16:; This routine performs the operation DEHL = BC * DE" + NL +
                "\tOrg\t8000h" + NL +
                "MUL16\tDI" + NL +
                "\tld\thl,0" + NL +
                "\tld\ta, 16" + NL +
                "MUL16LOOP:\tadd\thl, hl" + NL +
                "\trl\te" + NL +
                "\trl\td" + NL +
                "\tjp\tnc, NOMUL16" + NL +
                "\tadd\thl, bc" + NL +
                "\tjp\tnc, NOMUL16" + NL +
                "\tinc\tde ;This instruction (with the jump) is like an 'ADC DE,0'" + NL +
                "NOMUL16" + NL +
                "\tdec\ta" + NL +
                "\tjp\tnz, MUL16LOOP" + NL +
                "\tret" + NL +
                "entry\tld\tbc,12345" + NL +
                "\tld\tde,34567" + NL +
                "\tcall\tMUL16" + NL +
                "\tld\t(LOW), HL" + NL +
                "\tpush\tde" + NL +
                "\tpop\thl" + NL +
                "\tld\t(HIGH), HL" + NL +
                "\thalt" + NL +
                "\tORG\t9000h" + NL +
                "LOW\tDW\t0" + NL +
                "HIGH\tDS\t2" + NL +
                "\tEND";

            await StartToBasic();
            var assembly = computer.Assemble(program);
            assembly.Write(Path.GetTempFileName());
            var cmdFile = assembly.ToCmdFile();
            computer.LoadCMDFile(cmdFile);

            Assert.IsTrue(assembly.AssembledOK, "Failed to assemble");
        }
    }
}
