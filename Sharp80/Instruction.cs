/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Diagnostics;

namespace Sharp80.Processor
{
    internal sealed class Instruction
    {
        public delegate void InstDelegate();

        private readonly string name;
        private readonly string mnemonic;
        private readonly byte size;
        private readonly byte opSize;
        private readonly byte opInitSize;
        private readonly byte[] op = new byte[4];
        private readonly bool isPrefix = false;

        private readonly InstDelegate exec;

        private readonly bool hasReplaceableTokens = false;

        private readonly byte rIncrement;
        private readonly byte tStates;
        private readonly byte tStatesAlt;
        private readonly ushort ticks;
        private readonly ushort ticksWithExtra;

        private readonly uint signature;
        private readonly uint paddedSig;

        private string operand0 = null;
        private string operand1 = null;
        private string operand2 = null;
        private int? numOperands = null;

        public InstDelegate Execute { get { return exec; } }

        public byte RIncrement { get { return rIncrement; } }
        public bool IsPrefix { get { return isPrefix; } }
        public byte TStates { get { return tStates; } }
        public byte TStatesAlt { get { return tStatesAlt; } }
        public ushort Ticks { get { return ticks; } }
        public ushort TicksWithExtra { get { return ticksWithExtra; } }
        public uint Signature { get { return signature; } }
        public uint PaddedSig { get { return paddedSig; } }
        public byte Op0 { get { return op[0]; } }
        public byte Op1 { get { return op[1]; } }
        public byte Op3 { get { return op[3]; } }

        public Instruction(string Name, byte Op0, byte TStates, InstDelegate exec)
            : this(Name, Op0, null, null, TStates, exec, 0)
        {
        }
        public Instruction(string Name, byte Op0, byte TStates, InstDelegate exec, bool IsPrefix) : this(Name, Op0, 4, exec)
        {
            // Don't "ADD" this instruction, just call this constructor

            Debug.Assert(IsPrefix);
            isPrefix = IsPrefix;

            this.rIncrement = 1;
        }
        public Instruction(string Name, byte Op0, byte TStates, InstDelegate exec, byte TStatesAlt)
            : this(Name, Op0, null, null, TStates, exec, TStatesAlt)
        {
        }
        public Instruction(string Name, byte Op0, byte? Op1, byte TStates, InstDelegate exec)
            : this(Name, Op0, Op1, null, TStates, exec, 0)
        {
        }
        public Instruction(string Name, byte Op0, byte? Op1, byte TStates, InstDelegate exec, byte TStatesAlt)
            : this(Name, Op0, Op1, null, TStates, exec, TStatesAlt)
        {
        }
        public Instruction(string Name, byte Op0, byte? Op1, byte? Op3, byte TStates, InstDelegate exec)
            : this(Name, Op0, Op1, Op3, TStates, exec, 0)
        {
        }
        private Instruction(string Name, byte Op0, byte? Op1, byte? Op3, byte TStates, InstDelegate exec, byte TStatesAlt)
        {
            name = Name;            
            mnemonic = Lib.FirstText(Name);
            
            op[0] = Op0;
            op[1] = Op1 ?? 0x00;
            op[2] = 0x00;
            op[3] = Op3 ?? 0x00;

            opInitSize = (byte)(1 + (Op1.HasValue ? 1 : 0));
            opSize = (byte)(opInitSize + (Op3.HasValue ? 1 : 0));

            size = opSize;

            if (opSize == 1)
            {
                signature = Op0;
                paddedSig = (uint)(op[0] << 16);
            }
            else if (opSize == 2)
            {
                signature = (uint)((op[0] << 8) | op[1]);
                paddedSig = signature << 8;
            }
            else
            {
                signature = (uint)((op[0] << 16) | (op[1] << 8) | (op[3]));
                paddedSig = signature;
            }

            bool hasDisp =      Name.Contains("+d");
            bool hasLiteral16 = Name.Contains("NN");
            bool hasLiteral8 =  !hasLiteral16 && Name.Contains(" N") && !Name.Contains(" NZ") & !Name.Contains(" NC");
            bool hasRelJump =   Name.Contains(" e");
            bool hasPortRefNum = Name.Contains("(N)"); 

            if (hasDisp)
                size++;
            if (hasLiteral16)
                size += 2;
            if (hasLiteral8)
                size++;
            if (hasRelJump)
                size++;
            if (hasPortRefNum)
                size++;

            hasReplaceableTokens = hasDisp || hasLiteral8 || hasLiteral16 || hasRelJump || hasPortRefNum;

            tStates = TStates;
            tStatesAlt = TStatesAlt;
            ticks = (ushort)(TStates * Clock.TICKS_PER_TSTATE);
            ticksWithExtra = (ushort)((TStates + TStatesAlt) * Clock.TICKS_PER_TSTATE);

            this.exec = exec;

            rIncrement = 1;

            if ((op[0] == 0xDD) || (op[0] == 0xFD) || (op[0] == 0xCB) || (op[0] == 0xED))
                rIncrement++;


            Debug.Assert(size > 0 && size <= 4);
            Debug.Assert(opSize > 0 && opSize <= size);

            Debug.Assert(!hasReplaceableTokens || size > opSize);

            Debug.Assert(Op1 != null || Op3 == null);
            Debug.Assert(Op1 == null || size >= 2); 
            Debug.Assert(Op3 == null || size == 4);
        }        

        public byte Size
        {
            get {return this.size; }
        }

        public byte OpcodeLength
        {
            get { return this.opSize; }
        }
        public byte OpcodeInitialLength
        {
            get { return this.opInitSize; }
        }
        public byte[] Bytes
        {
            get { return op; }
        }

        public string Name(IMemory Memory, ushort PC)
        {
            if (hasReplaceableTokens)
                return this.mnemonic + LiteralSub(Memory, PC, this.name.Substring(this.mnemonic.Length));
            else
                return this.name;
        }
        public string NameWithRelativeAddressesAsComments(IMemory Memory, ushort PC)
        {
            string s = Name(Memory, PC);

            int i = s.IndexOf(" {");

            if (i > 0)
                s = s.Substring(0, i).PadRight(12) + "; " + s.Substring(i + 2, 4); 

            return s;
        }

        public string Name() { return name; }
        public string Mnemonic { get { return mnemonic; } }

        public int NumOperands
        {
            get
            {
                if (!numOperands.HasValue)
                {
                    if (String.IsNullOrEmpty(Operand0))
                        numOperands = 0;
                    else if (String.IsNullOrEmpty(Operand1))
                        numOperands = 1;
                    else if (String.IsNullOrEmpty(Operand2))
                        numOperands = 2;
                    else
                        numOperands = 3;
                }
                return numOperands.Value;
            }
        }

        public string Operand0
        {
            get
            {
                if (operand0 != null)
                    return operand0;

                var s = name.Substring(mnemonic.Length);
                var commaLoc = s.IndexOf(',');

                if (commaLoc < 0)
                    return operand0 = s.Trim();
                else 
                    return operand0 = s.Substring(0, commaLoc).Trim();
            }
        }

        public string Operand1
        {
            get
            {
                if (operand1 != null)
                    return operand1;

                var s = name.Substring(mnemonic.Length);
                var commaLoc = s.IndexOf(',');

                if (commaLoc < 0)
                    return operand1 = String.Empty;
                else
                    return operand1 = s.Substring(commaLoc + 1).Trim();
            }
        }
        public string Operand2
        {
            get
            {
                // Only used in some undocumented compound instructions
                if (operand2 != null)
                    return operand2;

                var s = name.Substring(mnemonic.Length);
                var commaLoc = s.IndexOf(',');

                if (commaLoc < 0)
                    return operand2 = String.Empty;
                else
                {
                    commaLoc = s.IndexOf(',', commaLoc + 1);
                    if (commaLoc < 0)
                        return operand2 = String.Empty;
                    else
                        return operand2 = s.Substring(commaLoc + 1).Trim();
                }
            }
        }
        private string LiteralSub(IMemory Memory, ushort PC, string s)
        {
            if (s.Contains("NN"))
            {
                s = s.Replace("NN", Lib.CombineBytes(Memory[PC.Offset(OpcodeInitialLength)], Memory[PC.Offset(OpcodeInitialLength + 1)]).ToHexString());
            }
            else if (s.Contains("(N)"))
            {
                s = s.Replace("(N)", " (" + (Memory[PC.Offset(size - 1)]).ToHexString() + ")");
            }
            else if (s.Contains(" N") && (!s.Contains(" NZ")) & (!s.Contains(" NC")))
            {
                s = s.Replace(" N", " " + (Memory[PC.Offset(size - 1)]).ToHexString());
            }

            if (s.Contains("+d"))
            {
                byte b = Memory[PC.Offset(OpcodeInitialLength)];
                s = s.Replace("+d", b.ToTwosCompHexString());
            }
            else if (s.Contains(" e"))
            {
                byte b = Memory[PC.Offset(OpcodeInitialLength)];
                sbyte x = b.TwosComp();
                s = s.Replace(" e", " " + b.ToTwosCompHexString() + " {" + PC.Offset(size + x).ToHexString() + "}");
            }

            return s;
        }
        
        public override string ToString()
        {
            return name;
        }
    }
}
