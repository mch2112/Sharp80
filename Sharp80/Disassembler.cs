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
            while (Memory[end] == 0 && end > Start) // NOP
                end--;
            if (end < 0xFFFF)
                end++;

            if (end > Sharp80.Memory.MEMORY_SIZE - 0x10)
                end = Sharp80.Memory.MEMORY_SIZE - 1;

            if (end > End)
                end = End;

            var sb = new StringBuilder(Sharp80.Memory.MEMORY_SIZE * 40);

            var li = new Dictionary<ushort, Instruction>();

            while (PC <= end)
            {
                li.Add(PC, inst = InstructionSet.GetInstruction(Memory[PC], Memory[PC + 1], Memory[PC + 3]));
                PC += inst.Size;
            }

            var header = $"; Disassembly from memory {Start:X4}H to {End:X4}H" +
                           Environment.NewLine;

            if (MakeAssemblable)
                return header +
                       string.Join(Environment.NewLine, li.Select(i => i.Value.AssemblableName(Memory, i.Key)));
            else
                return header +
                           string.Join(Environment.NewLine, li.Select(i => string.Format("{0}  {1}  {2}",
                                                                                         i.Key.ToHexString(),
                                                                                         Lib.GetSpacedHex(Memory, i.Key, i.Value.Size),
                                                                                         i.Value.FullName(Memory, i.Key)
                                                                                         )));
        }
    }
}