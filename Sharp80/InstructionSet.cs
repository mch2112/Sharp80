using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80.Processor
{
    internal sealed class InstructionSet
    {
        public Instruction NOP { get; private set; }

        private readonly SortedDictionary<uint, Instruction> instructions = new SortedDictionary<uint, Instruction>();

        private readonly Instruction[] STD, CB, DD, ED, FD, DDCB, FDCB;
        
        private Instruction DDPrefixNOP;
        private Instruction FDPrefixNOP;

        public InstructionSet()
        {
            instructions = new SortedDictionary<uint, Instruction>();

            STD = new Instruction[0x100];
            CB = new Instruction[0x100];
            DD = new Instruction[0x100];
            ED = new Instruction[0x100];
            FD = new Instruction[0x100];
            DDCB = new Instruction[0x100];
            FDCB = new Instruction[0x100];
        }

        public SortedDictionary<uint, Instruction> Instructions
        {
            get { return instructions; }
        }

        public Instruction GetInstruction(byte b, byte b2, byte b3)
        {
            Instruction i = STD[b];

            if (i == null)
            {
                switch (b)
                {
                    case 0xDD:
                        if (b2 == 0xCB)
                            i = DDCB[b3];
                        else
                            i = DD[b2];

                        i = i ?? DDPrefixNOP;

                        break;
                    case 0xFD:
                        if (b2 == 0xCB)
                            i = FDCB[b3];
                        else
                            i = FD[b2];

                        i = i ?? FDPrefixNOP;

                        break;
                    case 0xED:
                        i = ED[b2];
                        break;
                    case 0xCB:
                        i = CB[b2];
                        break;
                }
            }
            return i ?? NOP;
        }

        public Instruction GetInstruction(IMemory Memory, ushort Address)
        {
            byte b = Memory[Address++];

            Instruction i;

            switch (b)
            {
                case 0xDD:
                    i = ((b = Memory[Address++]) == 0xCB ? DDCB[Memory[++Address]] : DD[b]) ?? DDPrefixNOP;
                    break;
                case 0xFD:
                    i = ((b = Memory[Address++]) == 0xCB ? FDCB[Memory[++Address]] : FD[b]) ?? FDPrefixNOP;
                    break;
                case 0xED:
                    i = ED[Memory[Address]];
                    break;
                case 0xCB:
                    i = CB[Memory[Address]];
                    break;
                default:
                    i = STD[b];
                    break;
            }

            return i ?? NOP;
        }

        public string GetInstructionSetReport()
        {
            string sep = "+-------------+-------------------+----------+--------+";

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(sep);
            sb.AppendLine("| Hex         | Instruction       | T-States | Alt TS |");
            sb.AppendLine(sep);

            var li = instructions.Select(i => i.Value).OrderBy(i => i.PaddedSig).ToList();

            foreach (Instruction i in li)
            {
                sb.AppendFormat("| {0} {1} {2} {3} | {4,-18}|    {5:0#}    |   {6}   |   \r\n",
                                 i.Op0.ToHexString(),
                                 i.Size < 2 ? "  " : i.OpcodeLength < 2 ? "XX" : i.Op1.ToHexString(),
                                 i.Size < 3 ? "  " : "XX",
                                 i.Size < 4 ? "  " : i.OpcodeLength < 3 ? "XX" : i.Op3.ToHexString(),
                                 i.Name(),
                                 i.TStates,
                                 ((i.TStatesAlt > 0) ? (i.TStates + i.TStatesAlt).ToString("0#") : "  "));
            }

            sb.AppendLine(sep);

            return sb.ToString();

        }

        public void LoadTables()
        {
            foreach (KeyValuePair<uint, Instruction> kvp in instructions)
            {
                switch (kvp.Value.PaddedSig >> 8)
                {
                    case 0xDDCB:
                        DDCB[(kvp.Value.PaddedSig & 0xFF)] = kvp.Value;
                        break;
                    case 0xFDCB:
                        FDCB[(kvp.Value.PaddedSig & 0xFF)] = kvp.Value;
                        break;
                    default:
                        switch (kvp.Value.PaddedSig >> 16)
                        {
                            case 0xCB:
                                CB[(kvp.Value.PaddedSig >> 8) & 0xFF] = kvp.Value;
                                break;
                            case 0xDD:
                                DD[(kvp.Value.PaddedSig >> 8) & 0xFF] = kvp.Value;
                                break;
                            case 0xED:
                                ED[(kvp.Value.PaddedSig >> 8) & 0xFF] = kvp.Value;
                                break;
                            case 0xFD:
                                FD[(kvp.Value.PaddedSig >> 8) & 0xFF] = kvp.Value;
                                break;
                            default:
                                STD[kvp.Value.PaddedSig >> 16] = kvp.Value;
                                break;
                        }
                        break;
                }
            }

            this.NOP = STD[0x00];

            for (byte b = 0; b < 0xFF; b++)
            {
                ED[b] = ED[b] ?? new Instruction("NOP", 0xED, b, 8, NOP.Execute);
            }

            DDPrefixNOP = new Instruction("NOP", 0xDD, 4, NOP.Execute, true);
            FDPrefixNOP = new Instruction("NOP", 0xFD, 4, NOP.Execute, true);
        }
        public void Add(Instruction i)
        {
            instructions.Add(i.Signature, i);
        }
    }
}
