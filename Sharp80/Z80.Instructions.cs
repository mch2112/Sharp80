/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Diagnostics;

namespace Sharp80.Processor
{
    internal sealed partial class Z80 : IZ80_Status
    {
        private static readonly byte[] BIT = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        private static readonly byte[] NOT = { 0xFE, 0xFD, 0xFB, 0xF7, 0xEF, 0xDF, 0xBF, 0x7F };

        private void load(IRegister<byte> r1, IRegister<byte> r2) => r1.val = r2.val;
        private void load(IRegister<byte> r1, IRegisterIndexed r2)
        {
            load(r1, (IRegister<byte>)r2);
            WZ.val = r2.OffsetAddress;
        }
        private void load(IRegisterIndexed r1, IRegister<byte> r2)
        {
            r1.val = r2.val;
            WZ.val = r1.OffsetAddress;
        }
        private void load<T>(IRegister<T> r1, T Val) where T:struct => r1.val = Val;
        private void load_reg_nn(IRegister<byte> r) => r.val = ByteAtPCPlusInitialOpCodeLength;
        private void load_ixy_nn(IRegisterIndexed r)
        {
            r.val = ByteAtPCPlusOpCodeInitialLengthPlusOne;
            WZ.val = r.OffsetAddress;
        }
        private void load_a_mmmm()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;
            A.val = Memory[addr];
            WZ.val = addr;
            WZ.inc();
        }
        private void load_mmmm_a()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;
            Memory[addr] = A.val;
            
            WZ.val = (ushort)((A.val << 8) | ((addr + 1) & 0xFF));

            //Note for *BM1: MEMPTR_low = (addr + 1) & #FF,  MEMPTR_hi = 0
        }
        private void load<T>(IRegister<T> r1, IRegister<T> r2) where T:struct => r1.val = r2.val;
        private void load_reg_nnnn(IRegister<ushort> r) => r.val = WordAtPCPlusInitialOpcodeLength;
        private void load_ixy_mmmm(IRegister<ushort> r)
        {
            r.val = Memory.GetWordAt(WordAtPCPlusInitialOpcodeLength);
            // WZ?
        }
        private void load_mmmm_ixy(IRegister<ushort> r)
        {
            Memory.SetWordAt(WordAtPCPlusInitialOpcodeLength, r.val);
            // WZ?
        }
        private void load_xx_mmmm(IRegister<ushort> XX)
        {
            var addr = WordAtPCPlusInitialOpcodeLength;
            XX.val = Memory.GetWordAt(addr);
            WZ.val = addr;
            WZ.inc();
        }
        private void load_mmmm_xx(IRegister<ushort> XX)
        {
            var addr = WordAtPCPlusInitialOpcodeLength;
            Memory.SetWordAt(WordAtPCPlusInitialOpcodeLength, XX.val);
            WZ.val = addr;
            WZ.inc();
        }
        private void load_a_i()
        {
            A.val = I.val;
            F.val = (byte)((F.val & S_CF) | SZ53(A.val));
            VF = IFF2;
        }
        private void load_a_r()
        {
            A.val = R.val;
            F.val = (byte)((F.val & S_CF) | SZ53(A.val));
            VF = IFF2;
        }

        private void ldi()
        {
            ldx();
            DE.inc();
            HL.inc();
        }
        private void ldd()
        {
            ldx();
            DE.dec();
            HL.dec();
        }
        private void ldx()
        {
            byte b = HLM.val;

            DEM.val = b;
            BC.dec();
            b = (byte)((b + A.val) & 0xFF);

            F.val &= (S_SF | S_ZF | S_CF);

            VF = BC.NZ;
            F5 = b.IsBitSet(1);
            F3 = b.IsBitSet(3);
        }
        private void ldir()
        {
            ldi();
            if (BC.NZ)
            {
                NextPC -= 2;
                RecordExtraTicks = true;
            }
            else
            {
                WZ.val = PC.val;
                WZ.inc();
            }
        }
        private void lddr()
        {
            ldd();
            if (BC.NZ)
            {
                NextPC -= 2;
                RecordExtraTicks = true;
            }
            else
            {
                WZ.val = PC.val;
                WZ.inc();
            }
        }

        private void push(IRegister<ushort> r) => PushWord(r.val);
        private void pop(IRegister<ushort> r) => r.val = PopWord();

        private void cpir()
        {
            cpi();
            cpxr();
        }
        private void cpdr()
        {
            cpd();
            cpxr();
        }
        private void cpxr()
        {
            /*
             * https://www.omnimaga.org/asm-language/bit-n-(hl)-flags/5/?wap2
             * 
             * WZ: when BC=1 or A=(HL): exactly as CPI/R
	         * In other cases WZ = PC + 1 on each step, where PC = instruction address.
	         * Note since at the last execution BC=1 or A=(HL), resulting MEMPTR = PC + 1 + 1 
	         * (if there were not interrupts during the execution) 
             */

            if (BC.NZ && !ZF)
            {
                WZ.val = PC.val;
                WZ.inc();
                RecordExtraTicks = true;
                NextPC -= 2;
                Debug.Assert(PC.val == NextPC);
            }
        }
        private void cpi()
        {
            WZ.inc();
            cpx();
            HL.inc();
        }
        private void cpd()
        {
            WZ.dec();
            cpx();
            HL.dec();
        }
        private void cpx()
        {
            BC.dec();

            byte diff = (byte)((A.val - HLM.val) & 0xFF);
            bool cf = CF;

            F.val = SZ(diff);
            NF = true;
            CF = cf;
            VF = BC.NZ;

            if ((A.val & 0x0F) < (diff & 0x0F))
            {
                HF = true;
                diff--;
            }
            
            F5 = diff.IsBitSet(1);
            F3 = diff.IsBitSet(3);
        }

        private void halt()
        {
            // TODO: Push the address of the next instruction after the halt.
            // need to prevent additional pushes since this is implemented as a loop of halts
            halted = true;
            NextPC = PC.val;
        }
        private void daa()
        {
            // When this instruction is executed, the A register is BCD corrected using the contents of the flags. The exact
            // process is the following: if the least significant four bits of A contain a non-BCD digit (i. e. it is greater
            // than 9) or the H flag is set, then 0x06 is added to the register. Then the four most significant bits are
            // checked. If this more significant digit also happens to be greater than 9 or the C flag is set, then $60 is added.
            // If the second addition was needed, the C flag is set after execution, otherwise it is reset. The N flag is
            // preserved, P/V is parity and the others are altered by definition.

            bool cf = CF;
            bool nf = NF;
            bool hf = HF;

            int lowNibble = A.val & 0x0F;
            int highNibble = A.val >> 0x04;

            int diff;

            if (cf)
            {
                diff = (!hf && (lowNibble < 0x0A)) ? 0x60 : 0x66;
            }
            else
            {
                if (lowNibble > 0x09)
                    diff = (highNibble < 0x09) ? 0x06 : 0x66;
                else if (highNibble > 0x09)
                    diff = hf ? 0x66 : 0x60;
                else
                    diff = hf ? 0x06 : 0x00;
            }

            if (nf)
                A.val -= (byte)diff;
            else
                A.val += (byte)diff;

            F.val = SZ53P(A.val);
            NF = nf;
            CF = (cf || ((lowNibble < 0x0A) ? (highNibble > 0x09) : (highNibble > 0x08)));
            HF = (nf ? (hf && (lowNibble < 0x06)) : (lowNibble > 0x09));
        }
        private void cpl()
        {
            A.val ^= 0xFF;

            HF = true;
            NF = true;

            F5 = (A.val & S_F5) == S_F5;
            F3 = (A.val & S_F3) == S_F3;
        }
        private void neg()
        {
            byte temp = A.val;
            A.val = 0;
            sub(temp);
        }
        private void ccf()
        {
            HF = CF;
            CF = !CF;

            F5 = (A.val & S_F5) == S_F5;
            F3 = (A.val & S_F3) == S_F3;

            NF = false;
        }
        private void scf()
        {
            HF = false;
            NF = false;
            CF = true;

            F5 = (A.val & S_F5) == S_F5;
            F3 = (A.val & S_F3) == S_F3;
        }

        private void bitHLM(int shift)
        {
            bit(HLM.val, shift);
            F3 = WZ.val.IsBitSet(11);
            F5 = WZ.val.IsBitSet(13);
        }
        private void bit(IRegister<byte> r, int shift) => bit(r.val, shift);
        private void bit(IRegisterIndexed r, int shift)
        {
            bit(r.val, shift);

            var off = r.OffsetAddress;
            var ixdh = off >> 8;

            F3 = (ixdh & S_F3) == S_F3;
            F5 = (ixdh & S_F5) == S_F5;

            WZ.val = off;
        }
        private void bit(byte val, int shift)
        {
            F.val &= (R_NF & R_F3 & R_F5 & R_SF);
            F.val |= S_HF;

            ZF = ((val >> shift) & BIT_0_MASK) == 0x00;
            VF = ZF;

            if (NZ && shift == 7)
                F.val |= S_SF;

            F3 = (val & S_F3) == S_F3;
            F5 = (val & S_F5) == S_F5;
        }

        private void set(IRegister<byte> r, byte bit) => r.val |= BIT[bit];
        private void res(IRegister<byte> r, byte bit) => r.val &= NOT[bit];

        private void set(IRegisterIndexed r, byte b) { r.val |= BIT[b]; WZ.val = r.OffsetAddress; }
        private void res(IRegisterIndexed r, byte b) { r.val &= NOT[b]; WZ.val = r.OffsetAddress; }

        private void add(IRegister<byte> r) => add(r.val);
        private void add(IRegisterIndexed r)
        {
            add(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void add(byte val)
        {
            int a = A.val;
            int sum = a + val;
	
            F.val = SZ53(sum & 0xFF);
            CF = sum > 0xFF;
            HF = ((a & 0x0F) + (val & 0x0F)) > 0x0F;

            VF = ((a ^ val ^ 0x80) & (val ^ sum) & 0x80) == 0x80;

            A.val = (byte)sum;
        }
        private void add_n() => add(ByteAtPCPlusInitialOpCodeLength);
        private void add(IRegister<ushort> r1, IRegister<ushort> r2)
        {
            WZ.val = r1.val;
            WZ.inc();

            int sum = r1.val + r2.val;

    	    F.val = (byte)(F.val & (S_SF | S_ZF | S_VF));

            HF = ((r1.val & 0x0FFF) + (r2.val & 0x0FFF)) > 0x0FFF;

            F5 = ((sum >> 8) & S_F5) == S_F5;
            F3 = ((sum >> 8) & S_F3) == S_F3;
            CF = sum > 0xFFFF;
            
            r1.val = (ushort)sum;
        }

        private void adc(IRegister<byte> r) => adc(r.val);
        private void adc(IRegisterIndexed r)
        {
            adc(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void adc_n() => adc(ByteAtPCPlusInitialOpCodeLength);
        private void adc(byte val)
        {
            int a = A.val;
            int cfVal = CF ? 1 : 0;
            int sum = a + val + cfVal;

            F.val = SZ53(sum);
            
            HF = (a & 0x0F) + (val & 0x0F) + cfVal > 0x0F;
            Debug.Assert(HF == (((a ^ sum ^ val) & S_HF) == S_HF));
            
            CF = sum > 0xFF;
            VF = ((a ^ val ^ 0x80) & (val ^ sum) & 0x80) == 0x80;

            A.val = (byte)sum;
        }
        private void adc(IRegister<ushort> r1, IRegister<ushort> r2)
        {
            WZ.val = r1.val;
            WZ.inc();

            int cfVal = CF ? 1 : 0;
            int sum = r1.val + r2.val + cfVal;

            F.val = (byte)((sum >> 8) & (S_SF | S_F5 | S_F3));

            HF = ((r1.val & 0x0FFF) + (r2.val & 0x0FFF) + cfVal) > 0x0FFF;
            Debug.Assert(HF == ((((r1.val ^ sum ^ r2.val) >> 8) & S_HF) != 0));

            VF = ((r1.val ^ r2.val ^ 0x8000) & (r2.val ^ sum) & 0x8000) == 0x8000;
            ZF = (sum & 0xFFFF) == 0;
            CF = sum > 0xFFFF;

            r1.val = (ushort)sum;
        }

        private void sub(IRegister<byte> r) => sub(r.val);
        private void sub(IRegisterIndexed r)
        {
            sub(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void sub_n() => sub(ByteAtPCPlusInitialOpCodeLength);
        private void sub(byte val)
        {
            int a = A.val;
            int diff = a - val;

            A.val = (byte)diff;
            F.val = (byte)(SZ53(diff & 0xFF) | S_NF);

            HF = (a & 0x0F) - (val & 0x0F) < 0;

            Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));
            
            CF = diff < 0;
            VF = ((val ^ a) & (a ^ diff) & 0x80) == 0x80;
        }

        private void sbc(IRegister<byte> r) => sbc(r.val);
        private void sbc(IRegisterIndexed r)
        {
            sbc(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void sbc_n() => sbc(ByteAtPCPlusInitialOpCodeLength);
        private void sbc(byte val)
        {
            int a = A.val;
            int cfVal = CF ? 1 : 0;
            int diff = a - val - cfVal;

            A.val = (byte)diff;
	        F.val = (byte)(SZ53(diff) | S_NF);

            HF = (a & 0x0F) - (val & 0x0F) - cfVal < 0;
            
            Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));

            CF = diff < 0;
            VF = ((a ^ val) & (a ^ diff) & 0x80) == 0x80;
        }
        private void sbc(IRegister<ushort> r1, IRegister<ushort> r2)
        {
            Debug.Assert(r1.Equals(HL));

            WZ.val = r1.val;
            WZ.inc();

            int cfVal = CF ? 1 : 0;
            int diff = r1.val - r2.val - cfVal;
       
            F.val = (byte)(S_NF | (diff >> 8) & (S_SF | S_F5 | S_F3));
            HF = ((r1.val & 0x0FFF) - (r2.val & 0x0FFF)) - cfVal < 0;

            Debug.Assert(HF == ((((r1.val ^ diff ^ r2.val) >> 8) & S_HF) != 0x00));

            VF = ((r2.val ^ r1.val) & (r1.val ^ diff) & 0x8000) != 0x0000;
            ZF = diff == 0;
            CF = diff < 0;

            r1.val = (ushort)diff;
        }

        private void inc(IRegister<byte> r)
        {
            r.inc();

            F.val = (byte)(F53(r.val) | (F.val & S_CF));
            Debug.Assert(!NF);
            VF = r.val == 0x80;
            HF = (r.val & 0x0F) == 0;
            ZF = r.val == 0;
            SF = (r.val & 0x80) == 0x80;
        }
        private void dec(IRegister<byte> r)
        {
            r.dec();

            F.val = (byte)(F53(r.val) | (F.val & S_CF));
            NF = true;
            SF = (r.val & 0x80) == 0x80;
            ZF = r.val == 0;
            HF = (r.val & 0x0F) == 0x0F;
            VF = r.val == 0x7F;
        }
        private void inc(IRegister<ushort> r) => r.inc();
        private void dec(IRegister<ushort> r) => r.dec();

        private void and(IRegister<byte> r) => and(r.val);
        private void and(IRegisterIndexed r)
        {
            and(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void and_n() => and(ByteAtPCPlusInitialOpCodeLength);
        private void and(byte b)
        {
            A.val &= b;
            F.val = (byte)(SZ53P(A.val) | S_HF);
        }
        
        private void or(IRegister<byte> r) => or(r.val);
        private void or(IRegisterIndexed r)
        {
            or(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void or_n() => or(ByteAtPCPlusInitialOpCodeLength);
        private void or(byte b)
        {
            A.val |= b;
            F.val = SZ53P(A.val);
        }

        private void xor(IRegister<byte> r) => xor(r.val);
        private void xor(IRegisterIndexed r)
        {
            xor(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void xor_n() => xor(ByteAtPCPlusInitialOpCodeLength);
        private void xor(byte b)
        {
            A.val ^= b;
            F.val = SZ53P(A.val);
        }

        private void cp(IRegister<byte> r) => cp(r.val);
        private void cp(IRegisterIndexed r)
        {
            cp(r.val);
            WZ.val = r.OffsetAddress;
        }
        private void cp_n()
        {
            cp(ByteAtPCPlusInitialOpCodeLength);
        }
        private void cp(byte val)
        {
            int a = A.val;
            int diff = a - val;

            F.val = (byte)(SZ(diff & 0xFF) | S_NF);

            HF = (A.val & 0x0F) - (val & 0x0F) < 0;

            Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));

            VF = ((((a ^ val) & (a ^ diff)) >> 5) & S_VF) == S_VF;
            CF = diff < 0;

            F.val |= F53(val);
        }

        private void im(int mode)
        {
            switch (mode)
            {
                case 0:
                    InterruptMode = 0;
                    break;
                case 1:
                    InterruptMode = 1;
                    break;
                case 2:
                    InterruptMode = 2;
                    break;
            }
        }
        private void di()
        {
            Log.LogDebug("Disable interrupts");
            IFF1 = false;
            IFF2 = false;
        }
        private void ei()
        {
            Log.LogDebug("Enable interrupts");
            RestoreInterrupts = true;
            restoreInterruptsNow = false;
        }

        private void call()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;

            PushWord(NextPC);
            NextPC = addr;
            WZ.val = addr;
        }
        private void call(bool ConditionMet)
        {
            if (ConditionMet)
            {
                RecordExtraTicks = true;
                call();
            }
            else
            {
                // set WZ anyway
                WZ.val = WordAtPCPlusInitialOpcodeLength;
            }
        }

        private void ret()
        {
            NextPC = PopWord();
            WZ.val = NextPC;
            if (SteppedOut == false)
                SteppedOut = true;
        }
        private void ret(bool ConditionMet)
        {
            if (ConditionMet)
            {
                RecordExtraTicks = true;
                ret();
            }
        }
        private void retin()
        {
            // Same for RETI and RETN because apparently RETI also copies IFF2 to IFF1.

            Log.LogDebug($"Return from Interrupt, IFF1 {IFF1} -> {IFF2}");

            IFF1 = IFF2;
            ret();
        }
        private void rst(byte addr)
        {
            PushWord(NextPC);
            NextPC = addr;
            
            WZ.val = NextPC;
        }

        private void jp()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;

            NextPC = addr;
            WZ.val = addr;
        }
        private void jp(bool ConditionMet)
        {
            if (ConditionMet)
            {
                RecordExtraTicks = true;
                jp();
            }
            else
            {
                // Set WZ anyway
                WZ.val = WordAtPCPlusInitialOpcodeLength;
            }
        }
        private void jp(IRegister<ushort> r) => NextPC = r.val;

        private void jr()
        {
            NextPC = PC.val.Offset(2 + ByteAtPCPlusInitialOpCodeLength.TwosComp());
            WZ.val = NextPC;
        }
        private void jr(bool ConditionMet)
        {
            if (ConditionMet)
            {
                RecordExtraTicks = true;
                jr();
            }
        }

        private void djnz()
        {
            B.dec();
            if (B.NZ)
            {
                RecordExtraTicks = true;
                NextPC = PC.val.Offset(2 + ByteAtPCPlusInitialOpCodeLength.TwosComp());
                WZ.val = NextPC;
            }
        }

        private void rlca()
        {
            A.val = (byte)((A.val << 1) | (A.val >> 7));
            F.val = (byte)((F.val & (S_SF | S_ZF | S_VF)) | (A.val & (S_F5 | S_F3 | S_CF)));
        }
        private void rla()
        {
            byte val = A.val;
            val <<= 1;

            if (CF)
                val |= 0x01;

            F.val = (byte)((F.val & (S_SF | S_ZF | S_VF)) | (val & (S_F5 | S_F3)));
            CF = (A.val & 0x80) == 0x80;
            A.val = val;
        }
        private void rrca()
        {
            bool cf = ((A.val & 0x01) == 0x01);

            A.val = (byte)((A.val >> 1) | (A.val << 7));
            F.val &= (S_SF | S_ZF | S_VF);

            CF = cf;
            F.val |= F53(A.val);
        }
        private void rra()
        {
            int newA = A.val >> 1;

            if (CF)
                newA |= 0x80;

            F.val = (byte)(F.val & (S_SF | S_ZF | S_VF));
            CF = (A.val & 0x01) == 0x01;
            F.val |= F53(newA);

            A.val = (byte)newA;
        }
        private void rlc(IRegister<byte> r)
        {
            int oldVal = r.val;
            bool cf = ((oldVal & 0x80) == 0x80);
            byte newVal = (byte)((oldVal << 1) | (oldVal >> 7));

            r.val = newVal;
            F.val = SZ53P(newVal);
            CF = cf;
        }
        private void rlc(IRegisterIndexed r)
        {
            rlc((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void rl(IRegister<byte> r)
        {
            bool cf = ((r.val & 0x80) == 0x80);
            int newVal = (r.val << 1);
            if (CF)
                newVal |= 0x01;
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void rl(IRegisterIndexed r)
        {
            rl((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void rrc(IRegister<byte> r)
        {
            int oldVal = r.val;
            bool cf = (oldVal & 0x01) == 0x01;
            byte newVal = (byte)((oldVal >> 1) | (oldVal << 7));
            
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = newVal;
        }
        private void rrc(IRegisterIndexed r)
        {
            rrc((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void rr(IRegister<byte> r)
        {
            bool cf = (r.val & 0x01) == 0x01;
            int newVal = (r.val >> 1);
            if (CF)
                newVal |= 0x80;
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void rr(IRegisterIndexed r)
        {
            rr((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void sla(IRegister<byte> r)
        {
            bool cf = (r.val & 0x80) == 0x80;
            int newVal = (r.val << 1);
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void sla(IRegisterIndexed r)
        {
            sla((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void sll(IRegister<byte> r)
        {
            bool cf = (r.val & 0x80) == 0x80;
            int newVal = (r.val << 1) | 0x01;
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void sll(IRegisterIndexed r)
        {
            sll((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void sra(IRegister<byte> r)
        {
            bool cf = (r.val & 0x01) == 0x01;
            int newVal = ((r.val >> 1) | (r.val & 0x80));
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void sra(IRegisterIndexed r)
        {
            sra((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void srl(IRegister<byte> r)
        {
            bool cf = (r.val & 0x01) == 0x01;
            int newVal = (r.val >> 1);
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void srl(IRegisterIndexed r)
        {
            srl((IRegister<byte>)r);
            WZ.val = r.OffsetAddress;
        }
        private void rld()
        {
            int oldHlm = HLM.val;
            int newHlm = (oldHlm << 4) | (A.val & 0x0F);

            HLM.val = (byte)newHlm;
            A.val   = (byte)((A.val & 0xF0) | (oldHlm >> 4));
            F.val   = (byte)((F.val & 0x01) | SZ53P(A.val));

            WZ.val  = HL.val;
            WZ.inc();
        }
        private void rrd()
        {
            int oldHlm = HLM.val;
            int newHlm = (oldHlm >> 4) | (A.val << 4);

            A.val   = (byte)((A.val & 0xF0) | (oldHlm & 0x0F));
            HLM.val = (byte)newHlm;
            F.val   = (byte)((F.val & 0x01) | SZ53P(A.val));

            WZ.val = HL.val;
            WZ.inc();
        }

        private void exx()
        {
            ex(BC, BCp);
            ex(DE, DEp);
            ex(HL, HLp);
        }
        private void ex(IRegister<ushort> r1, IRegister<ushort> r2)
        {
            ushort temp = r1.val;
            r1.val = r2.val;
            r2.val = temp;
        }
        private void ex_spm(IRegister<ushort> r2)
        {
            ushort temp = SPM.val;
            SPM.val = r2.val;
            r2.val = temp;
            WZ.val = r2.val;
        }

        private void inir()
        {
            ini();
            inxr();
        }
        private void indr()
        {
            ind();
            inxr();
        }
        private void inxr()
        {
            if (B.NZ)
            {
                NextPC -= 2;
                RecordExtraTicks = true;
            }
        }
        private void ini()
        {
            WZ.val = BC.val;
            WZ.inc();
            inx(true);
        }
        private void ind()
        {
            WZ.val = BC.val;
            WZ.dec();
            inx(false);
        }
        private void inx(bool IncHL)
        {
            HLM.val = InPort(C.val);
            dec(B);
            NF = (HLM.val & 0x80) != 0x00;

            if (IncHL)
                HL.inc();
            else
                HL.dec();

            int k = ((C.val + 0xFF) & 0xFF) + HLM.val;

            if (k > 0xFF)
            {
                CF = true;
                HF = true;
            }

            VF = P((k & 0x07) ^ B.val) != 0;
        }

        private void otir()
        {
            outi();
            if (B.NZ)
            {
                NextPC -= 2;
                RecordExtraTicks = true;
            }
        }
        private void otdr()
        {
            outd();
            if (B.val != 0x00)
            {
                NextPC -= 2;
                RecordExtraTicks = true;
            }
        }
        private void outi()
        {
            outx(true);
            WZ.val = BC.val;
            WZ.inc();
        }
        private void outd()
        {
            outx(false);
            WZ.val = BC.val;
            WZ.dec();
        }
        private void outx(bool IncHL)
        {
            OutPort(C.val, HLM.val);

            dec(B);

            if (IncHL)
                HL.inc();
            else
                HL.dec();

            int k = L.val + HLM.val;
            
            if (k > 0xFF)
            {
                CF = true;
                HF = true;
            }

            VF = P((k & 0x07) ^ B.val) != 0;
        }
        
        // COMPOUND INSTRUCTIONS

        private void rlc_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); rlc(r2); load(r1, r2);
        }
        private void rrc_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); rrc(r2); load(r1, r2);
        }
        private void rl_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); rl(r2); load(r1, r2);
        }
        private void rr_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); rr(r2); load(r1, r2);
        }
        private void sla_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); sla(r2); load(r1, r2);
        }
        private void sra_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); sra(r2); load(r1, r2);
        }
        private void sll_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); sll(r2); load(r1, r2);
        }
        private void srl_compound(IRegister<byte> r1, IRegister<byte> r2)
        {
            load(r2, r1); srl(r2); load(r1, r2);
        }
        private void res_compound(IRegister<byte> r1, IRegister<byte> r2, byte bit)
        {
            load(r2, r1); res(r2, bit); load(r1, r2);
        }
        private void set_compound(IRegister<byte> r1, IRegister<byte> r2, byte bit)
        {
            load(r2, r1); set(r2, bit); load(r1, r2);
        }
    }
}
