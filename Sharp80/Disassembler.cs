using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.Processor
{
    internal class Disassembler
    {
        public const int NUM_DISASSEMBLY_LINES = 22;

        public bool HistoricDisassemblyMode { get; set; }
        private Z80.InstructionSet InstructionSet { get; set; }

        public Disassembler(Z80.InstructionSet InstructionSet) => this.InstructionSet = InstructionSet;
        
        public string Disassemble(IReadOnlyList<byte> Memory, ushort Start, ushort End, bool MakeAssemblable)
        {
            ushort PC = Start;
            Instruction inst;

            var end = End;

            // Eliminate trailing NOPs
            while (end > Start && Memory[end - 1] == 0) // NOP
                end--;

            if (end > Z80.MEMORY_SIZE - 0x10)
                end = Z80.MEMORY_SIZE - 1;

            var sb = new StringBuilder(Z80.MEMORY_SIZE * 40);

            var li = new Dictionary<ushort, Instruction>();

            while (PC <= end)
            {
                li.Add(PC, inst = InstructionSet.GetInstruction(Memory[PC], Memory[PC + 1], Memory[PC + 3]));
                PC += inst.Size;
            }

            var header = $"; Disassembly from memory {Start:X4}H to {end:X4}H" +
                           Environment.NewLine;

            if (MakeAssemblable)
                return header +
                       String.Join(Environment.NewLine, li.Select(i => i.Value.AssemblableName(Memory, i.Key)));
            else
                return header +
                       String.Join(Environment.NewLine, li.Select(i => string.Format("{0}  {1}  {2}",
                                                                                         i.Key.ToHexString(),
                                                                                         Lib.GetSpacedHex(Memory, i.Key, i.Value.Size),
                                                                                         i.Value.FullName(Memory, i.Key)
                                                                                         )));
        }
    }
}