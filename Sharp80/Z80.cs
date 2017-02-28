/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharp80.Processor
{
    internal sealed partial class Z80
    {
        public const int NUM_DISASSEMBLY_LINES = 22;

        // REGISTERS

        public Register16Normal PC, SP, WZ;
        public Register8 A, F, B, C, D, E, H, L, I, R, Ap, Fp, Bp, Cp, Dp, Ep, Hp, Lp;
        public Register16Compound IX, IY, BC, DE, HL, AF, BCp, DEp, HLp, AFp;

        private Register8Indirect BCM, DEM, HLM;
        private Register8Indexed IXM, IYM;
        private Register16Indirect SPM;

        // FLAGS

        public bool CF { get { return (F.val & S_CF) != 0; } set { if (value) F.val |= S_CF; else F.val &= R_CF; } }	// carry flag 
        public bool NF { get { return (F.val & S_NF) != 0; } set { if (value) F.val |= S_NF; else F.val &= R_NF; } }	// add / subtract flag
        public bool VF { get { return (F.val & S_VF) != 0; } set { if (value) F.val |= S_VF; else F.val &= R_VF; } }	// parity / overflow flag
        public bool F3 { get { return (F.val & S_F3) != 0; } set { if (value) F.val |= S_F3; else F.val &= R_F3; } }	// not used
        public bool HF { get { return (F.val & S_HF) != 0; } set { if (value) F.val |= S_HF; else F.val &= R_HF; } }	// half-carry flag
        public bool F5 { get { return (F.val & S_F5) != 0; } set { if (value) F.val |= S_F5; else F.val &= R_F5; } }	// not used
        public bool ZF { get { return (F.val & S_ZF) != 0; } set { if (value) F.val |= S_ZF; else F.val &= R_ZF; } }	// zero flag
        public bool SF { get { return (F.val & S_SF) != 0; } set { if (value) F.val |= S_SF; else F.val &= R_SF; } }	// sign flag

        public bool NZ { get { return (F.val & S_ZF) == 0; } }
        public bool NC { get { return (F.val & S_CF) == 0; } }
        public bool PE { get { return (F.val & S_VF) != 0; } set { if (value) F.val |= S_VF; else F.val &= R_VF; } }  // parity even, same as overflow
        public bool PO { get { return (F.val & S_VF) == 0; } set { if (value) F.val &= R_VF; else F.val |= S_VF; } }  // parity odd, opposite of PE / V

        private const byte BIT_0_MASK = 0x01, BIT_1_MASK = 0x02, BIT_2_MASK = 0x04, BIT_3_MASK = 0x08,
                           BIT_4_MASK = 0x10, BIT_5_MASK = 0x20, BIT_6_MASK = 0x40, BIT_7_MASK = 0x80;

        private const byte BIT_0_INV_MASK = 0xFF - BIT_0_MASK, BIT_1_INV_MASK = 0xFF - BIT_1_MASK, BIT_2_INV_MASK = 0xFF - BIT_2_MASK, BIT_3_INV_MASK = 0xFF - BIT_3_MASK,
                           BIT_4_INV_MASK = 0xFF - BIT_4_MASK, BIT_5_INV_MASK = 0xFF - BIT_5_MASK, BIT_6_INV_MASK = 0xFF - BIT_6_MASK, BIT_7_INV_MASK = 0xFF - BIT_7_MASK;

        private const byte S_CF = BIT_0_MASK, S_NF = BIT_1_MASK, S_VF = BIT_2_MASK, S_F3 = BIT_3_MASK,
                           S_HF = BIT_4_MASK, S_F5 = BIT_5_MASK, S_ZF = BIT_6_MASK, S_SF = BIT_7_MASK;

        private const byte R_CF = BIT_0_INV_MASK, R_NF = BIT_1_INV_MASK, R_VF = BIT_2_INV_MASK, R_F3 = BIT_3_INV_MASK,
                           R_HF = BIT_4_INV_MASK, R_F5 = BIT_5_INV_MASK, R_ZF = BIT_6_INV_MASK, R_SF = BIT_7_INV_MASK;

        private Computer computer;
        private PortSet ports;
        private IMemory memory;

        public IMemory Memory { get { return memory; } }

        public Instruction CurrentInstruction { get; private set; }
        
        public string Assemble()
        {
            return new Assembler.Assembler(this.InstructionSet).Assemble();
        }

        // INTERRUPTS

        public bool IFF1 { get; set; }
        public bool IFF2 { get; set; }
        public bool RestoreInterrupts { get; set; }
        private bool RecordExtraTicks { get; set; }
        public ushort NextPC { get; private set; }

        private byte interruptMode;
        private bool restoreInterruptsNow;
        private byte im2Vector = 0xFF;         // For IM2 only
        private bool halted;
        private ushort breakPoint = 0;
        
        // User vectors

        public ushort BreakPoint
        {
            get { return breakPoint; }
            set
            {
                if (breakPoint != value)
                {
                    breakPoint = value;
                    skipOneBreakpoint = PC.val == value && !computer.IsRunning;
                }
            }
        }
        public bool BreakPointOn { get; set; } = false;
        private ushort? systemBreakPoint = null;
        private bool skipOneBreakpoint = false;

        // Circular history buffer
        private ushort[] historyBuffer = new ushort[NUM_DISASSEMBLY_LINES];
        private int historyBufferCursor = 0;
        private ulong historyInstructionCount = 0;

        // CONSTRUCTOR

        public Z80(Computer Computer, PortSet Ports)
        {
            computer = Computer;

            memory = new Memory();

            ports = Ports;

            PC = new Register16Normal(this, "PC");
            SP = new Register16Normal(this, "SP");

            I = new Register8(this, "I");
            R = new Register8(this, "R");

            A = new Register8(this, "A");
            F = new Register8(this, "F");
            B = new Register8(this, "B");
            C = new Register8(this, "C");
            D = new Register8(this, "D");
            E = new Register8(this, "E");
            H = new Register8(this, "H");
            L = new Register8(this, "L");

            Ap = new Register8(this, "A'");
            Fp = new Register8(this, "F'");
            Bp = new Register8(this, "B'");
            Cp = new Register8(this, "C'");
            Dp = new Register8(this, "D'");
            Ep = new Register8(this, "E'");
            Hp = new Register8(this, "H'");
            Lp = new Register8(this, "L'");

            IX = new Register16Compound(this, "IX");
            IY = new Register16Compound(this, "IY");

            BC = new Register16Compound(C, B, this, "BC");
            DE = new Register16Compound(E, D, this, "DE");
            HL = new Register16Compound(L, H, this, "HL");
            AF = new Register16Compound(F, A, this, "AF");

            BCp = new Register16Compound(Cp, Bp, this, "BC'");
            DEp = new Register16Compound(Ep, Dp, this, "DE'");
            HLp = new Register16Compound(Lp, Hp, this, "HL'");
            AFp = new Register16Compound(Fp, Ap, this, "AF'");

            WZ = new Register16Normal(this, "WZ");

            BCM = new Register8Indirect(this, BC, "(BC)");
            DEM = new Register8Indirect(this, DE, "(DE)");
            HLM = new Register8Indirect(this, HL, "(HL)");
            IXM = new Register8Indexed(this, IX, "(IX)");
            IYM = new Register8Indexed(this, IY, "(IY)");
            SPM = new Register16Indirect(this, SP, "(SP)");

            InitInstructionSet();
            Reset();

            this.CurrentInstruction = instructionSet.NOP; // NOP
        }
        static Z80()
        {
            InitFlagsString();
        }

        // PROPERTIES

        public byte InterruptMode
        {
            get { return interruptMode; }
            set { interruptMode = value; }
        }
        public bool HistoricDisassemblyMode { get; set; }
        public byte ByteAtPCPlusInitialOpCodeLength
        {
            get
            {
                return Memory[PC.val.Offset(CurrentInstruction.OpcodeInitialLength)];
            }
        }
        public byte ByteAtPCPlusOpCodeInitialLengthPlusOne
        {
            get
            {
                return Memory[PC.val.Offset(CurrentInstruction.OpcodeInitialLength + 1)];
            }
        }
        public ushort WordAtPCPlusInitialOpcodeLength
        {
            get
            {
                Debug.Assert(CurrentInstruction.OpcodeInitialLength == CurrentInstruction.OpcodeLength);
                return Memory.GetWordAt(PC.val.Offset(CurrentInstruction.OpcodeInitialLength));
            }
        }

        public IEnumerable<Instruction> InstructionSet
        {
            get
            {
                foreach (var kvp in instructionSet.Instructions)
                {
                    yield return kvp.Value;
                }
            }
        }

        // MAIN EXECUTION CONTROL

        public void Reset()
        {
            // During reset time, the address bus and data bus go to a high impadance state and all control
            // output signals go to the inactive state.

            interruptMode = 0;
            IFF1 = false;
            IFF2 = false;
            RestoreInterrupts = false;
            restoreInterruptsNow = false;

            halted = false;

            PC.val  = 0x0000;
            SP.val  = 0x0000;

            AF.val  = 0x0000;
            BC.val  = 0x0000;
            DE.val  = 0x0000;
            HL.val  = 0x0000;

            AFp.val = 0x0000;
            BCp.val = 0x0000;
            DEp.val = 0x0000;
            HLp.val = 0x0000;

            IX.val  = 0xFFFF;
            IY.val  = 0xFFFF;

            I.val   = 0x00;
            R.val   = 0x00;

            WZ.val = 0x0000;

            ZF = true;

            historyInstructionCount = 0;
            UpdatePCHistory();
        }

        // returns ticks used
        public ulong Exec()
        {
            ulong retVal;

            if (BreakPointOn && (PC.val == BreakPoint))
            {
                // skip breakpoint so we don't break before we get started again
                if (skipOneBreakpoint)
                {
                    skipOneBreakpoint = false;
                }
                else
                {
                    skipOneBreakpoint = true;
                    computer.Stop(WaitForStop: false);
                    return 0;
                }
            }
            if (systemBreakPoint.HasValue && (PC.val == systemBreakPoint.Value))
            {
                systemBreakPoint = null;
                computer.Stop(WaitForStop: false);
                return 0;
            }

            if (Log.TraceOn)
                Log.LogTrace(GetLineInfo(PC.val));

            CurrentInstruction = GetInstructionAt(PC.val);
            retVal = ExecuteInstruction(CurrentInstruction);

            if (RestoreInterrupts)
            {
                if (restoreInterruptsNow)
                {
                    IFF1 = true;
                    IFF2 = true;
                    RestoreInterrupts = false;
                    restoreInterruptsNow = false;
                }
                else
                {
                    restoreInterruptsNow = true;
                }
            }
            UpdatePCHistory();
            historyInstructionCount++;
            return retVal;
        }
        
        public void StepOver()
        {
            systemBreakPoint = PC.val.Offset(GetInstructionAt(PC.val).Size);
        }
        public void StepOut()
        {
            systemBreakPoint = PeekWord();
        }
        public void CancelStepOverOrOut()
        {
            systemBreakPoint = null;
        }
        public void Jump(ushort Address)
        {
            if (PC.val != Address)
                PC.val = Address;
        }
        // Returns ticks used
        private ushort ExecuteInstruction(Instruction Instruction)
        {
            try
            {
                // returns ticks used

                Debug.Assert(this.RecordExtraTicks == false);

                NextPC = PC.val;
                NextPC += Instruction.Size;

                Debug.Assert(this.RecordExtraTicks == false);

                Instruction.Execute();

                PC.val = NextPC;

                IncrementR(Instruction.RIncrement);
            }
            catch (Exception ex)
            {
                Log.LogException(ex);
            }
            if (this.RecordExtraTicks)
            {
                this.RecordExtraTicks = false;
                return Instruction.TicksWithExtra;
            }
            else
            {
                return Instruction.Ticks;
            }
        }

        private Instruction GetInstructionAt(ushort Address)
        {
            return this.instructionSet.GetInstruction(memory, Address);
        }

        // INTERRUPTS

        public ulong Interrupt()
        {
            // TODO: ensure no interrupts between strings of DD or FD prefixes.

            if (CanInterrupt)
            {
                Log.LogDebug(string.Format("CPU Interrupt. IFF1: {0} IFF2: {1}", IFF1, IFF2));

                IFF1 = false;
                IFF2 = false;

                IncrementR(1);

                if (halted)
                {
                    PushWord(PC.val.Offset(2));
                    halted = false;
                }
                else
                {
                    PushWord(PC.val);
                }

                switch (interruptMode)
                {
                    case 1:
                        WZ.val = PC.val = 0x0038;
                        return 13000;
                    case 2:
                        WZ.val = PC.val = (ushort)(I.val * 0x100 + im2Vector);
                        return 19000;
                    default:
                        Log.LogDebug(string.Format("Interrupt Mode {0} Not Supported", interruptMode));
                        return 0;
                }
            }
            else
            {
                return 0;
            }
        }
        public bool CanInterrupt
        {
            get { return IFF1 && !CurrentInstruction.IsPrefix; }
        }
        public bool CanNmi
        {
            get { return !CurrentInstruction.IsPrefix; }
        }
        public void NonMaskableInterrupt()
        {
            Log.LogDebug("Non Maskable Interrupt exec, IFF1 False");

            IFF1 = false;   // Leave IFF2 alone to restore IFF1 after the NMI
            if (halted)
            {
                PushWord(PC.val.Offset(2));
                halted = false;
            }
            else
            {
                PushWord(PC.val);
            }
            PC.val = 0x0066;
            WZ.val = 0x0066;
            IncrementR(1);
            UpdatePCHistory();
            historyInstructionCount++;
        }
 
        // REFRESH REGISTER

        public void IncrementR(byte num)
        {
            // Memory refresh register; sometimes used for pseudo-random numbers. Don't change bit 7.
            R.val = (byte)((R.val & 0x80) | ((R.val + num) & 0x7F));
        }

        // DISPLAY SUPPORT

        private void UpdatePCHistory()
        {
            ++historyBufferCursor;
            historyBufferCursor %= NUM_DISASSEMBLY_LINES;
            historyBuffer[historyBufferCursor] = PC.val;
        }

        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(PC.val);
            Writer.Write(SP.val);

            Writer.Write(BC.val);
            Writer.Write(DE.val);
            Writer.Write(HL.val);
            Writer.Write(AF.val);

            Writer.Write(BCp.val);
            Writer.Write(DEp.val);
            Writer.Write(HLp.val);
            Writer.Write(AFp.val);
            
            Writer.Write(IX.val);
            Writer.Write(IY.val);

            Writer.Write(I.val);
            Writer.Write(R.val);

            Writer.Write(WZ.val);
            Writer.Write(NextPC);

            Writer.Write(interruptMode);
            Writer.Write(IFF1);
            Writer.Write(IFF2);
            Writer.Write(RestoreInterrupts);
            Writer.Write(RecordExtraTicks);
            Writer.Write(restoreInterruptsNow);
            Writer.Write(im2Vector);
            Writer.Write(halted);
            Writer.Write(breakPoint);
            Writer.Write(BreakPointOn);
            Writer.Write(skipOneBreakpoint);
            Writer.Write(systemBreakPoint.HasValue);
            Writer.Write(systemBreakPoint ?? 0);

            Writer.Write(historyBufferCursor);
            Writer.Write(historyInstructionCount);
            for (int i = 0; i < NUM_DISASSEMBLY_LINES; i++)
                Writer.Write(historyBuffer[i]);

            ports.Serialize(Writer);
            memory.Serialize(Writer);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            PC.val = Reader.ReadUInt16();
            SP.val = Reader.ReadUInt16();

            BC.val = Reader.ReadUInt16();
            DE.val = Reader.ReadUInt16();
            HL.val = Reader.ReadUInt16();
            AF.val = Reader.ReadUInt16();

            BCp.val = Reader.ReadUInt16();
            DEp.val = Reader.ReadUInt16();
            HLp.val = Reader.ReadUInt16();
            AFp.val = Reader.ReadUInt16();

            IX.val = Reader.ReadUInt16();
            IY.val = Reader.ReadUInt16();

            I.val = Reader.ReadByte();
            R.val = Reader.ReadByte();

            WZ.val = Reader.ReadUInt16();
            NextPC = Reader.ReadUInt16();

            interruptMode = Reader.ReadByte();
            IFF1 = Reader.ReadBoolean();
            IFF2 = Reader.ReadBoolean();
            RestoreInterrupts = Reader.ReadBoolean();
            RecordExtraTicks = Reader.ReadBoolean();
            restoreInterruptsNow = Reader.ReadBoolean();
            im2Vector = Reader.ReadByte();
            halted = Reader.ReadBoolean();
            breakPoint = Reader.ReadUInt16();
            BreakPointOn = Reader.ReadBoolean();
            skipOneBreakpoint = Reader.ReadBoolean();
            if (Reader.ReadBoolean())
            {
                systemBreakPoint = Reader.ReadUInt16();
            }
            else
            {
                systemBreakPoint = null;
                Reader.ReadUInt16();
            }

            historyBufferCursor = Reader.ReadInt32();
            historyInstructionCount = Reader.ReadUInt64();
            for (int i = 0; i < NUM_DISASSEMBLY_LINES; i++)
                historyBuffer[i] = Reader.ReadUInt16();

            ports.Deserialize(Reader);
            memory.Deserialize(Reader);
        }

        // INSTRUCTION SUPPORT
        private void InitInstructionSet()
        {
            SetupInstructionObjects();
            instructionSet.LoadTables();
        }
        private void OutPort(byte pornNum, byte value) 
        {
            ports[pornNum] = value;
        }
        private void OutPortR(Register8 r)
        {
            OutPort(C.val, r.val);
        }
        private void OutPortZero()
        {
            OutPort(C.val, (byte)0);
        }
        private void OutPortN()
        {
            byte aVal = A.val;
            byte portNum = ByteAtPCPlusInitialOpCodeLength;
            
            OutPort(portNum, aVal);

            // Note for *BM1: WZ_low = (port + 1) & #FF,  WZ_hi = 0
            portNum++;
            WZ.setVal(aVal, portNum);
        }
        private void OutPortA()
        {
            OutPort(C.val, A.val);
            WZ.val = BC.val;
            WZ.inc();
        }
        
        private byte InPort(byte pornNum)
        {
            byte inp = ports[pornNum];
            return inp;
        }
        private byte InPortC()
        {
            byte b = InPort(C.val);
            F.val = (byte)((F.val & S_CF) | SZ53P(b));
            return b;
        }
        private void InPortR(Register8 r)
        {
            r.val = InPortC();
        }
        private void InPortZero() 
        {
            InPortC();
        }
        private void InPortN()
        {
            byte aVal = A.val;
            byte portNum = ByteAtPCPlusInitialOpCodeLength;

            A.val = InPort(portNum);

            portNum++;
            WZ.setVal(aVal, portNum);
        }
        private void InPortA()
        {
            InPortR(A);
            WZ.val = BC.val;
            WZ.inc();
        }

        private void PushWord(ushort val)
        {
            SP.val -= 2;
            SPM.val = val;
        }
        private ushort PopWord()
        {
            ushort val = SPM.val;
            SP.val += 2;
            return val;
        }
        private ushort PeekWord()
        {
            return SPM.val;
        }

        private byte SZ(byte input)    { return Lib.SZ[input]; }
        private byte SZ53P(byte input) { return Lib.SZ53P[input]; }
        private byte SZ53(byte input)  { return Lib.SZ53[input]; }
        private byte F53(byte input)   { return Lib.F53[input]; }
        private byte P(byte input)     { return Lib.P[input]; }

        private byte SZ(int input)    { return Lib.SZ[input & 0xFF]; }
        private byte SZ53P(int input) { return Lib.SZ53P[input & 0xFF]; }
        private byte SZ53(int input)  { return Lib.SZ53[input & 0xFF]; }
        private byte F53(int input)   { return Lib.F53[input & 0xFF]; }
        private byte P(int input)     { return Lib.P[input & 0xFF]; }
    }
}