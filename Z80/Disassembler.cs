using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.Z80
{
    public enum DisassemblyMode { Normal, WithAscii, Assemblable }

    internal class Disassembler
    {
        public const int NUM_DISASSEMBLY_LINES = 22;

        public bool HistoricDisassemblyMode { get; set; }
        private Z80.Z80InstructionSet instructionSet;

        public Disassembler(Z80.Z80InstructionSet InstructionSet) => instructionSet = InstructionSet;

        public string Disassemble(IReadOnlyList<byte> Memory, ushort Start, ushort End, DisassemblyMode Mode)
        {
            Instruction inst;

            ushort end = Math.Max(Start, End);

            // Eliminate trailing NOPs
            while (end > Start + 0x10 && Memory[end - 1] == 0) // NOP
                end--;

            if (end > Z80.MEMORY_SIZE - 0x10)
                end = Z80.MEMORY_SIZE - 1;

            var sb = new StringBuilder(Z80.MEMORY_SIZE * 40);

            var li = new Dictionary<ushort, Instruction>();

            int PC = Start;
            while (PC <= end)
            {
                li.Add((ushort)PC, inst = instructionSet.GetInstruction(Memory[(ushort)PC], Memory[(ushort)(PC + 1)], Memory[(ushort)(PC + 3)]));
                PC += inst.Size;
            }

            var header = $"; Disassembly from memory {Start:X4}H to {end:X4}H" + Environment.NewLine;

            switch (Mode)
            {
                case DisassemblyMode.WithAscii:
                    return header +
                       String.Join(Environment.NewLine, li.Select(i => string.Format("{0}  {1}  {2}",
                                                                                         i.Key.ToHexString(),
                                                                                         Lib.GetSpacedHex(Memory, i.Key, i.Value.Size),
                                                                                         i.Value.FullName(Memory, i.Key)
                                                                                         ).PadRight(40) + GetAscii(i.Value, i.Key, Memory)));
                case DisassemblyMode.Assemblable:
                    return header +
                           String.Join(Environment.NewLine, li.Select(i => i.Value.AssemblableName(Memory, i.Key)));
                default:
                    return header +
                       String.Join(Environment.NewLine, li.Select(i => string.Format("{0}  {1}  {2}",
                                                                                         i.Key.ToHexString(),
                                                                                         Lib.GetSpacedHex(Memory, i.Key, i.Value.Size),
                                                                                         i.Value.FullName(Memory, i.Key)
                                                                                         )));
            }
        }
        private static string GetAscii(Instruction i, ushort Address, IReadOnlyList<byte> Memory)
        {
            string ret = String.Empty;
            for (int j = 0; j < i.Size; j++)
                ret += Memory[(ushort)(Address + j)].AsAscii();
            return ret;
        }
    }
}