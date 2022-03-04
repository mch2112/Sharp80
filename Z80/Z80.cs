/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Diagnostics;

namespace Sharp80.Z80
{
    public partial class Z80
    {
        public const int MEMORY_SIZE = 0x10000;

        // changing this requires new SERIALIZATION_VERSION
        public const int NUM_DISASSEMBLY_LINES = 22;

        private Disassembler disassembler;
        private readonly CircularBuffer historyBuffer;

        // REGISTERS

        private readonly IRegister<ushort> PC;
        private readonly IRegister<ushort> SP;
        private readonly IRegister<ushort> WZ;
        private readonly IRegister<byte> A, F, B, C, D, E, H, L, I, R, Ap, Fp, Bp, Cp, Dp, Ep, Hp, Lp;
        private readonly RegisterCompound IX, IY, BC, DE, HL, AF, BCp, DEp, HLp, AFp;

        private readonly IRegister<byte> BCM, DEM, HLM;
        private readonly IRegisterIndexed IXM, IYM;
        private readonly IRegister<ushort> SPM;

        // FLAGS

        private bool CF { get => (F.Value & S_CF) != 0; set { if (value) F.Value |= S_CF; else F.Value &= R_CF; } }	// carry flag 
        private bool NF { get => (F.Value & S_NF) != 0; set { if (value) F.Value |= S_NF; else F.Value &= R_NF; } }	// add / subtract flag
        private bool VF { get => (F.Value & S_VF) != 0; set { if (value) F.Value |= S_VF; else F.Value &= R_VF; } }	// parity / overflow flag
        private bool F3 { get => (F.Value & S_F3) != 0; set { if (value) F.Value |= S_F3; else F.Value &= R_F3; } }	// not used
        private bool HF { get => (F.Value & S_HF) != 0; set { if (value) F.Value |= S_HF; else F.Value &= R_HF; } }	// half-carry flag
        private bool F5 { get => (F.Value & S_F5) != 0; set { if (value) F.Value |= S_F5; else F.Value &= R_F5; } }	// not used
        private bool ZF { get => (F.Value & S_ZF) != 0; set { if (value) F.Value |= S_ZF; else F.Value &= R_ZF; } }	// zero flag
        private bool SF { get => (F.Value & S_SF) != 0; set { if (value) F.Value |= S_SF; else F.Value &= R_SF; } }	// sign flag

        private bool NZ => (F.Value & S_ZF) == 0;
        private bool NC => (F.Value & S_CF) == 0;
        private bool PE => (F.Value & S_VF) != 0;  // parity even, same flag as overflow
        private bool PO => (F.Value & S_VF) == 0;  // parity odd, opposite of PE / V

        private const byte BIT_0_MASK = 0x01, BIT_1_MASK = 0x02, BIT_2_MASK = 0x04, BIT_3_MASK = 0x08,
                           BIT_4_MASK = 0x10, BIT_5_MASK = 0x20, BIT_6_MASK = 0x40, BIT_7_MASK = 0x80;

        private const byte BIT_0_INV_MASK = 0xFF - BIT_0_MASK, BIT_1_INV_MASK = 0xFF - BIT_1_MASK, BIT_2_INV_MASK = 0xFF - BIT_2_MASK, BIT_3_INV_MASK = 0xFF - BIT_3_MASK,
                           BIT_4_INV_MASK = 0xFF - BIT_4_MASK, BIT_5_INV_MASK = 0xFF - BIT_5_MASK, BIT_6_INV_MASK = 0xFF - BIT_6_MASK, BIT_7_INV_MASK = 0xFF - BIT_7_MASK;

        private const byte S_CF = BIT_0_MASK, S_NF = BIT_1_MASK, S_VF = BIT_2_MASK, S_F3 = BIT_3_MASK,
                           S_HF = BIT_4_MASK, S_F5 = BIT_5_MASK, S_ZF = BIT_6_MASK, S_SF = BIT_7_MASK;

        private const byte R_CF = BIT_0_INV_MASK, R_NF = BIT_1_INV_MASK, R_VF = BIT_2_INV_MASK, R_F3 = BIT_3_INV_MASK,
                           R_HF = BIT_4_INV_MASK, R_F5 = BIT_5_INV_MASK, R_ZF = BIT_6_INV_MASK, R_SF = BIT_7_INV_MASK;

        private readonly IComputer computer;
        private readonly IPorts ports;

        internal IMemory Memory { get; private set; }
        internal Instruction CurrentInstruction { get; private set; }

        // INTERRUPTS

        public bool IFF1 { get; set; }
        public bool IFF2 { get; set; }
        public bool RestoreInterrupts { get; set; }
        public ushort NextPC { get; private set; }
        public byte Im2Vector { get; set; } = 0xFF;  // For IM2

        private bool RecordExtraTicks { get; set; }
        private bool restoreInterruptsNow;
        private bool halted;
        private ushort breakPoint = 0;

        // CONSTRUCTORS

        static Z80() => InitFlagsString();

        public Z80(IComputer Computer)
        {
            InstructionSet = new Z80InstructionSet();
            historyBuffer = new CircularBuffer(NUM_DISASSEMBLY_LINES);

            disassembler = new Disassembler(InstructionSet);

            computer = Computer;

            Memory = Computer.Memory;
            ports = Computer.Ports;

            PC = new Register16("PC");
            SP = new Register16("SP");

            I = new Register8("I");
            R = new Register8("R");

            A = new Register8("A");
            F = new Register8("F");
            B = new Register8("B");
            C = new Register8("C");
            D = new Register8("D");
            E = new Register8("E");
            H = new Register8("H");
            L = new Register8("L");

            Ap = new Register8("A'");
            Fp = new Register8("F'");
            Bp = new Register8("B'");
            Cp = new Register8("C'");
            Dp = new Register8("D'");
            Ep = new Register8("E'");
            Hp = new Register8("H'");
            Lp = new Register8("L'");

            IX = new RegisterCompound("IX");
            IY = new RegisterCompound("IY");

            BC = new RegisterCompound(C, B, "BC");
            DE = new RegisterCompound(E, D, "DE");
            HL = new RegisterCompound(L, H, "HL");
            AF = new RegisterCompound(F, A, "AF");

            BCp = new RegisterCompound(Cp, Bp, "BC'");
            DEp = new RegisterCompound(Ep, Dp, "DE'");
            HLp = new RegisterCompound(Lp, Hp, "HL'");
            AFp = new RegisterCompound(Fp, Ap, "AF'");

            WZ = new Register16("WZ");

            BCM = new Register8Indirect(this, BC, "(BC)");
            DEM = new Register8Indirect(this, DE, "(DE)");
            HLM = new Register8Indirect(this, HL, "(HL)");
            IXM = new RegisterIndexed(this, IX, "(IX)");
            IYM = new RegisterIndexed(this, IY, "(IY)");
            SPM = new Register16Indirect(this, SP, "(SP)");

            InitInstructionSet();
            Reset();

            CurrentInstruction = InstructionSet.NOP; // NOP

            historyBuffer.Add(0);
        }

        // PROPERTIES

        public byte InterruptMode { get; private set; }
        public bool HistoricDisassemblyMode { get; set; }

        public byte ByteAtPCPlusCoreOpCodeSize => Memory[PC.Value.Offset(CurrentInstruction.OpcodeCoreSize)];
        public byte ByteAtPCPlusCoreOpCodeSizePlusOne => Memory[PC.Value.Offset(CurrentInstruction.OpcodeCoreSize + 1)];
        public ushort WordAtPCPlusInitialOpcodeLength => Memory.GetWordAt(PC.Value.Offset(CurrentInstruction.OpcodeCoreSize));

        // MAIN EXECUTION CONTROL

        public void Reset()
        {
            // During reset time, the address bus and data bus go to a high impadance state and all control
            // output signals go to the inactive state.

            InterruptMode = 0;
            IFF1 = false;
            IFF2 = false;
            RestoreInterrupts = false;
            restoreInterruptsNow = false;

            halted = false;

            PC.Value = 0x0000;
            SP.Value = 0x0000;

            AF.Value = 0x0000;
            BC.Value = 0x0000;
            DE.Value = 0x0000;
            HL.Value = 0x0000;

            AFp.Value = 0x0000;
            BCp.Value = 0x0000;
            DEp.Value = 0x0000;
            HLp.Value = 0x0000;

            IX.Value = 0xFFFF;
            IY.Value = 0xFFFF;

            I.Value = 0x00;
            R.Value = 0x00;

            WZ.Value = 0x0000;

            ZF = true;

            historyBuffer.Clear();
        }
        /// <summary>
        /// Executes an instruction
        /// </summary>
        /// <returns>Number of ticks consumed</returns>
        public ulong Exec()
        {
            ulong retVal;

            if (BreakPointOn && (PC.Value == BreakPoint) && computer.IsRunning)
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
            if (systemBreakPoint.HasValue && (PC.Value == systemBreakPoint.Value))
            {
                systemBreakPoint = null;
                computer.Stop(WaitForStop: false);
                return 0;
            }
            if (SteppedOut == true)
            {
                SteppedOut = null;
                computer.Stop(WaitForStop: false);
                return 0;
            }

            CurrentInstruction = GetInstructionAt(PC.Value);

            if (TraceOn)
                retVal = TraceLog.Log(computer.ElapsedTStates, ExecuteInstruction);
            else
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
            historyBuffer.Add(PC.Value);
            return retVal;
        }

        /// <summary>
        /// Null: not waiting to step out; inactive
        /// False: waiting to step out on next RET
        /// True: stepped out (executed RET), pending stop
        /// </summary>
        public bool? SteppedOut { get; set; } = null;
        public bool StepOver()
        {
            var i = GetInstructionAt(PC.Value);
            if (IsSteppable(i))
            {
                systemBreakPoint = PC.Value.Offset(i.Size);
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Jump(ushort Address)
        {
            if (PC.Value != Address)
            {
                PC.Value = Address;
                historyBuffer.ReplaceLast(PC.Value);
            }
        }
        
        // Returns ticks used
        private ushort ExecuteInstruction(Instruction Instruction)
        {
            // returns ticks used

            Debug.Assert(RecordExtraTicks == false);

            NextPC = PC.Value;
            NextPC += Instruction.Size;

            Debug.Assert(RecordExtraTicks == false);

            Instruction.Execute();

            PC.Value = NextPC;

            IncrementR(Instruction.RIncrement);

            if (RecordExtraTicks)
            {
                RecordExtraTicks = false;
                return Instruction.TicksWithExtra;
            }
            else
            {
                return Instruction.Ticks;
            }
        }

        private Instruction GetInstructionAt(ushort Address) => InstructionSet.GetInstruction(Memory, Address);

        // BREAKPOINTS

        public ushort BreakPoint
        {
            get => breakPoint;
            set
            {
                if (breakPoint != value)
                {
                    breakPoint = value;
                    skipOneBreakpoint = PC.Value == value && !computer.IsRunning;
                }
            }
        }
        public bool BreakPointOn { get; set; } = false;
        private ushort? systemBreakPoint = null;
        private bool skipOneBreakpoint = false;

        // INTERRUPTS

        public ulong Interrupt()
        {
            if (CanInterrupt)
            {
                IFF1 = false;
                IFF2 = false;

                IncrementR(1);

                if (halted)
                {
                    PushWord(PC.Value.Offset(2));
                    halted = false;
                }
                else
                {
                    PushWord(PC.Value);
                }

                switch (InterruptMode)
                {
                    case 1:
                        WZ.Value = PC.Value = 0x0038;
                        return 13000;
                    case 2:
                        WZ.Value = PC.Value = (ushort)(I.Value * 0x100 + Im2Vector);
                        return 19000;
                    default:
                        return 0;
                }
            }
            else
            {
                return 0;
            }
        }
        public bool CanInterrupt => IFF1 && !CurrentInstruction.IsPrefix;
        public bool CanNmi => !CurrentInstruction.IsPrefix;

        public void NonMaskableInterrupt()
        {
            IFF2 = IFF1;
            IFF1 = false;

            if (halted)
            {
                PushWord(PC.Value.Offset(2));
                halted = false;
            }
            else
            {
                PushWord(PC.Value);
            }
            PC.Value = 0x0066;
            WZ.Value = 0x0066;
            IncrementR(1);
            historyBuffer.Add(PC.Value);
        }

        // REFRESH REGISTER

        public void IncrementR(byte num)
        {
            // Memory refresh register; sometimes used for pseudo-random numbers. Don't change bit 7.
            R.Value = (byte)((R.Value & 0x80) | ((R.Value + num) & 0x7F));
        }

        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(PC.Value);
            Writer.Write(SP.Value);

            Writer.Write(BC.Value);
            Writer.Write(DE.Value);
            Writer.Write(HL.Value);
            Writer.Write(AF.Value);

            Writer.Write(BCp.Value);
            Writer.Write(DEp.Value);
            Writer.Write(HLp.Value);
            Writer.Write(AFp.Value);

            Writer.Write(IX.Value);
            Writer.Write(IY.Value);

            Writer.Write(I.Value);
            Writer.Write(R.Value);

            Writer.Write(WZ.Value);
            Writer.Write(NextPC);

            Writer.Write(InterruptMode);
            Writer.Write(IFF1);
            Writer.Write(IFF2);
            Writer.Write(RestoreInterrupts);
            Writer.Write(RecordExtraTicks);
            Writer.Write(restoreInterruptsNow);
            Writer.Write(Im2Vector);
            Writer.Write(halted);
            Writer.Write(breakPoint);
            Writer.Write(BreakPointOn);
            Writer.Write(skipOneBreakpoint);
            Writer.Write(systemBreakPoint.HasValue);
            Writer.Write(systemBreakPoint ?? 0);

            historyBuffer.Serialize(Writer);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                PC.Value = Reader.ReadUInt16();
                SP.Value = Reader.ReadUInt16();

                BC.Value = Reader.ReadUInt16();
                DE.Value = Reader.ReadUInt16();
                HL.Value = Reader.ReadUInt16();
                AF.Value = Reader.ReadUInt16();

                BCp.Value = Reader.ReadUInt16();
                DEp.Value = Reader.ReadUInt16();
                HLp.Value = Reader.ReadUInt16();
                AFp.Value = Reader.ReadUInt16();

                IX.Value = Reader.ReadUInt16();
                IY.Value = Reader.ReadUInt16();

                I.Value = Reader.ReadByte();
                R.Value = Reader.ReadByte();

                WZ.Value = Reader.ReadUInt16();
                NextPC = Reader.ReadUInt16();

                InterruptMode = Reader.ReadByte();
                IFF1 = Reader.ReadBoolean();
                IFF2 = Reader.ReadBoolean();
                RestoreInterrupts = Reader.ReadBoolean();
                RecordExtraTicks = Reader.ReadBoolean();
                restoreInterruptsNow = Reader.ReadBoolean();
                Im2Vector = Reader.ReadByte();
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
                return historyBuffer.Deserialize(Reader, DeserializationVersion);
            }
            catch
            {
                return false;
            }
        }

        // TRACE

        private bool traceOn;
        public bool TraceOn
        {
            get => traceOn;
            set
            {
                if (traceOn != value)
                {
                    if (value)
                        TraceLog = new TraceLog(this);
                    traceOn = value;
                }
            }
        }
        public string Trace => TraceLog?.GetLogAndClear() ?? String.Empty;
        private TraceLog TraceLog { get; set;}

        // INSTRUCTION SUPPORT

        private void InitInstructionSet()
        {
            SetupInstructionObjects();
            InstructionSet.Initialize();
        }
        private void PushWord(ushort val)
        {
            SP.Value -= 2;
            SPM.Value = val;
        }
        private ushort PopWord()
        {
            ushort val = SPM.Value;
            SP.Value += 2;
            return val;
        }
        private bool IsSteppable(Instruction i)
        {
            return i.Mnemonic == "CALL" ||
                   i.Mnemonic == "RST" ||
                   i.Mnemonic == "LDIR" ||
                   i.Mnemonic == "LDDR" ||
                   i.Mnemonic == "CPIR" ||
                   i.Mnemonic == "CPDR" ||
                   i.Mnemonic == "OTIR" ||
                   i.Mnemonic == "OTDR" ||
                   i.Mnemonic == "INNR" ||
                   i.Mnemonic == "INDR" ||
                   i.Mnemonic == "DJNZ";
        }

        // ASSEMBLER / DISASSEMBLER

        public Assembler.Assembly Assemble(string SourceText) => new Assembler.Assembler(InstructionSet.Instructions.Values).Assemble(SourceText);
        public string Disassemble(ushort Start, ushort End, DisassemblyMode Mode) => disassembler.Disassemble(Memory, Start, End, Mode);

        // HELPERS

        private static byte SZ(byte input) =>    Lib.SZ[input];
        private static byte SZ53P(byte input) => Lib.SZ53P[input];
        private static byte SZ53(byte input) =>  Lib.SZ53[input];
        private static byte F53(byte input) =>   Lib.F53[input];
        private static byte P(byte input) =>     Lib.P[input];

        private static byte SZ(int input) =>     Lib.SZ[input & 0xFF];
        private static byte SZ53P(int input) =>  Lib.SZ53P[input & 0xFF];
        private static byte SZ53(int input) =>   Lib.SZ53[input & 0xFF];
        private static byte F53(int input) =>    Lib.F53[input & 0xFF];
        private static byte P(int input) =>      Lib.P[input & 0xFF];
    }
}