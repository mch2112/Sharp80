/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharp80.Processor
{
    internal sealed partial class Z80 : IZ80_Status
    {
        public const int NUM_DISASSEMBLY_LINES = 22;

        // REGISTERS

        private IRegister<ushort> PC, SP, WZ;
        private IRegister<byte> A, F, B, C, D, E, H, L, I, R, Ap, Fp, Bp, Cp, Dp, Ep, Hp, Lp;
        private IRegisterCompound IX, IY, BC, DE, HL, AF, BCp, DEp, HLp, AFp;

        private IRegister<byte> BCM, DEM, HLM;
        private IRegisterIndexed IXM, IYM;
        private IRegister<ushort> SPM;

        // FLAGS

        private bool CF { get { return (F.val & S_CF) != 0; } set { if (value) F.val |= S_CF; else F.val &= R_CF; } }	// carry flag 
        private bool NF { get { return (F.val & S_NF) != 0; } set { if (value) F.val |= S_NF; else F.val &= R_NF; } }	// add / subtract flag
        private bool VF { get { return (F.val & S_VF) != 0; } set { if (value) F.val |= S_VF; else F.val &= R_VF; } }	// parity / overflow flag
        private bool F3 { get { return (F.val & S_F3) != 0; } set { if (value) F.val |= S_F3; else F.val &= R_F3; } }	// not used
        private bool HF { get { return (F.val & S_HF) != 0; } set { if (value) F.val |= S_HF; else F.val &= R_HF; } }	// half-carry flag
        private bool F5 { get { return (F.val & S_F5) != 0; } set { if (value) F.val |= S_F5; else F.val &= R_F5; } }	// not used
        private bool ZF { get { return (F.val & S_ZF) != 0; } set { if (value) F.val |= S_ZF; else F.val &= R_ZF; } }	// zero flag
        private bool SF { get { return (F.val & S_SF) != 0; } set { if (value) F.val |= S_SF; else F.val &= R_SF; } }	// sign flag

        private bool NZ { get { return (F.val & S_ZF) == 0; } }
        private bool NC { get { return (F.val & S_CF) == 0; } }
        private bool PE { get { return (F.val & S_VF) != 0; } set { if (value) F.val |= S_VF; else F.val &= R_VF; } }  // parity even, same as overflow
        private bool PO { get { return (F.val & S_VF) == 0; } set { if (value) F.val &= R_VF; else F.val |= S_VF; } }  // parity odd, opposite of PE / V

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

        public IMemory Memory => memory;
        public Instruction CurrentInstruction { get; private set; }
        public Assembler.Assembly Assemble(string SourceText) => new Assembler.Assembler(instructionSet.Instructions.Values).Assemble(SourceText);

        // INTERRUPTS

        public bool IFF1 { get; set; }
        public bool IFF2 { get; set; }
        public bool RestoreInterrupts { get; set; }
        private bool RecordExtraTicks { get; set; }
        public ushort NextPC { get; private set; }

        private bool restoreInterruptsNow;
        private byte im2Vector = 0xFF;         // For IM2 only
        private bool halted;
        private ushort breakPoint = 0;
        
        // User vectors

        public ushort BreakPoint
        {
            get => breakPoint;
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
        private ulong instructionCount = 0;

        // CONSTRUCTORS

        static Z80()
        {
            InitFlagsString();
            instructionSet = new InstructionSet();
            Disassembler.Initialize(instructionSet);
        }

        public Z80(Computer Computer, PortSet Ports)
        {
            computer = Computer;

            memory = new Memory();

            ports = Ports;

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

            CurrentInstruction = instructionSet.NOP; // NOP
        }
        
        // PROPERTIES

        public byte InterruptMode { get; private set; }
        public bool HistoricDisassemblyMode { get; set; }
        public byte ByteAtPCPlusInitialOpCodeLength => Memory[PC.val.Offset(CurrentInstruction.OpcodeInitialLength)];
        public byte ByteAtPCPlusOpCodeInitialLengthPlusOne => Memory[PC.val.Offset(CurrentInstruction.OpcodeInitialLength + 1)];
        public ushort WordAtPCPlusInitialOpcodeLength => Memory.GetWordAt(PC.val.Offset(CurrentInstruction.OpcodeInitialLength));

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

            instructionCount = 0;
            UpdatePCHistory();
        }
        
        // returns ticks used
        public ulong Exec()
        {
            ulong retVal;

            if (BreakPointOn && (PC.val == BreakPoint) && computer.IsRunning)
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
            if (SteppedOut == true)
            {
                SteppedOut = null;
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
            instructionCount++;
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
            var i = GetInstructionAt(PC.val);
            if (IsSteppable(i))
            {
                systemBreakPoint = PC.val.Offset(i.Size);
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Jump(ushort Address)
        {
            if (PC.val != Address)
            {
                PC.val = Address;
                historyBuffer[historyBufferCursor] = PC.val;
            }
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
                ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser);
            }
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

        private Instruction GetInstructionAt(ushort Address) => instructionSet.GetInstruction(memory, Address);

        // INTERRUPTS

        public ulong Interrupt()
        {
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

                switch (InterruptMode)
                {
                    case 1:
                        WZ.val = PC.val = 0x0038;
                        return 13000;
                    case 2:
                        WZ.val = PC.val = (ushort)(I.val * 0x100 + im2Vector);
                        return 19000;
                    default:
                        Log.LogDebug(string.Format("Interrupt Mode {0} Not Supported", InterruptMode));
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
            Log.LogDebug("Non Maskable Interrupt exec, IFF1 False");

            IFF2 = IFF1;
            IFF1 = false;

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
            instructionCount++;
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
            // Atomic for thread safety
            if (historyBufferCursor == NUM_DISASSEMBLY_LINES - 1)
                historyBufferCursor = 0;
            else
                ++historyBufferCursor;

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

            Writer.Write(InterruptMode);
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
            Writer.Write(instructionCount);
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

            InterruptMode = Reader.ReadByte();
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
            instructionCount = Reader.ReadUInt64();
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
        private void OutPort(byte pornNum, byte value) => ports[pornNum] = value;
        private void OutPortR(IRegister<byte> r) => OutPort(C.val, r.val);
        private void OutPortZero() => OutPort(C.val, (byte)0);
        private void OutPortN()
        {
            byte aVal = A.val;
            byte portNum = ByteAtPCPlusInitialOpCodeLength;

            OutPort(portNum, aVal);

            // Note for *BM1: WZ_low = (port + 1) & #FF,  WZ_hi = 0
            portNum++;
            WZ.val = (ushort)((aVal << 8) | portNum);
        }
        private void OutPortA()
        {
            OutPort(C.val, A.val);
            WZ.val = BC.val;
            WZ.inc();
        }
        
        private byte InPort(byte pornNum) => ports[pornNum];
        private byte InPortC()
        {
            byte b = InPort(C.val);
            F.val = (byte)((F.val & S_CF) | SZ53P(b));
            return b;
        }
        private void InPortR(IRegister<byte> r) => r.val = InPortC();
        private void InPortZero() => InPortC();
        private void InPortN()
        {
            byte aVal = A.val;
            byte portNum = ByteAtPCPlusInitialOpCodeLength;

            A.val = InPort(portNum);

            portNum++;
            WZ.val = (ushort)((aVal << 8) | portNum);
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
        
        private bool IsSteppable(Instruction i)
        {
            return i.Mnemonic == "CALL" ||
                   i.Mnemonic == "RST"  ||
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
        private byte SZ(byte input)    => Lib.SZ[input];
        private byte SZ53P(byte input) => Lib.SZ53P[input];
        private byte SZ53(byte input)  => Lib.SZ53[input];
        private byte F53(byte input)   => Lib.F53[input];
        private byte P(byte input)     => Lib.P[input];

        private byte SZ(int input)     => Lib.SZ[input & 0xFF];
        private byte SZ53P(int input)  => Lib.SZ53P[input & 0xFF];
        private byte SZ53(int input)   => Lib.SZ53[input & 0xFF];
        private byte F53(int input)    => Lib.F53[input & 0xFF];
        private byte P(int input)      => Lib.P[input & 0xFF];
    }
}