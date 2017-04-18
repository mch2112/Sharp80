/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Diagnostics;

namespace Sharp80.Z80
{
    public partial class Z80 : IStatus
    {
        private static readonly byte[] BIT = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        private static readonly byte[] NOT = { 0xFE, 0xFD, 0xFB, 0xF7, 0xEF, 0xDF, 0xBF, 0x7F };

        private void load(IRegister<byte> r1, IRegister<byte> r2) => r1.Value = r2.Value;
        private void load(IRegister<byte> r1, IRegisterIndexed r2)
        {
            load(r1, (IRegister<byte>)r2);
            WZ.Value = r2.OffsetAddress;
        }
        private void load(IRegisterIndexed r1, IRegister<byte> r2)
        {
            r1.Value = r2.Value;
            WZ.Value = r1.OffsetAddress;
        }
        private void load<T>(IRegister<T> r1, T Val) where T : struct => r1.Value = Val;
        private void load_reg_nn(IRegister<byte> r) => r.Value = ByteAtPCPlusCoreOpCodeSize;
        private void load_ixy_nn(IRegisterIndexed r)
        {
            r.Value = ByteAtPCPlusCoreOpCodeSizePlusOne;
            WZ.Value = r.OffsetAddress;
        }
        private void load_a_mmmm()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;
            A.Value = Memory[addr];
            WZ.Value = addr;
            WZ.Inc();
        }
        private void load_mmmm_a()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;
            Memory[addr] = A.Value;

            WZ.Value = (ushort)((A.Value << 8) | ((addr + 1) & 0xFF));

            //Note for *BM1: MEMPTR_low = (addr + 1) & #FF,  MEMPTR_hi = 0
        }
        private void load<T>(IRegister<T> r1, IRegister<T> r2) where T : struct => r1.Value = r2.Value;
        private void load_reg_nnnn(IRegister<ushort> r) => r.Value = WordAtPCPlusInitialOpcodeLength;
        private void load_ixy_mmmm(IRegister<ushort> r)
        {
            r.Value = Memory.GetWordAt(WordAtPCPlusInitialOpcodeLength);
            // WZ?
        }
        private void load_mmmm_ixy(IRegister<ushort> r)
        {
            Memory.SetWordAt(WordAtPCPlusInitialOpcodeLength, r.Value);
            // WZ?
        }
        private void load_xx_mmmm(IRegister<ushort> XX)
        {
            var addr = WordAtPCPlusInitialOpcodeLength;
            XX.Value = Memory.GetWordAt(addr);
            WZ.Value = addr;
            WZ.Inc();
        }
        private void load_mmmm_xx(IRegister<ushort> XX)
        {
            var addr = WordAtPCPlusInitialOpcodeLength;
            Memory.SetWordAt(WordAtPCPlusInitialOpcodeLength, XX.Value);
            WZ.Value = addr;
            WZ.Inc();
        }
        private void load_a_i()
        {
            A.Value = I.Value;
            F.Value = (byte)((F.Value & S_CF) | SZ53(A.Value));
            VF = IFF2;
        }
        private void load_a_r()
        {
            A.Value = R.Value;
            F.Value = (byte)((F.Value & S_CF) | SZ53(A.Value));
            VF = IFF2;
        }

        private void ldi()
        {
            ldx();
            DE.Inc();
            HL.Inc();
        }
        private void ldd()
        {
            ldx();
            DE.Dec();
            HL.Dec();
        }
        private void ldx()
        {
            byte b = HLM.Value;

            DEM.Value = b;
            BC.Dec();
            b = (byte)((b + A.Value) & 0xFF);

            F.Value &= (S_SF | S_ZF | S_CF);

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
                WZ.Value = PC.Value;
                WZ.Inc();
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
                WZ.Value = PC.Value;
                WZ.Inc();
            }
        }

        private void push(IRegister<ushort> r) => PushWord(r.Value);
        private void pop(IRegister<ushort> r) => r.Value = PopWord();

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
                WZ.Value = PC.Value;
                WZ.Inc();
                RecordExtraTicks = true;
                NextPC -= 2;
                Debug.Assert(PC.Value == NextPC);
            }
        }
        private void cpi()
        {
            WZ.Inc();
            cpx();
            HL.Inc();
        }
        private void cpd()
        {
            WZ.Dec();
            cpx();
            HL.Dec();
        }
        private void cpx()
        {
            BC.Dec();

            byte diff = (byte)((A.Value - HLM.Value) & 0xFF);
            bool cf = CF;

            F.Value = SZ(diff);
            NF = true;
            CF = cf;
            VF = BC.NZ;

            if ((A.Value & 0x0F) < (diff & 0x0F))
            {
                HF = true;
                diff--;
            }

            F5 = diff.IsBitSet(1);
            F3 = diff.IsBitSet(3);
        }

        private void nop() { }
        private void halt()
        {
            // TODO: Push the address of the next instruction after the halt.
            // need to prevent additional pushes since this is implemented as a loop of halts
            halted = true;
            NextPC = PC.Value;
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

            int lowNibble = A.Value & 0x0F;
            int highNibble = A.Value >> 0x04;

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
                A.Value -= (byte)diff;
            else
                A.Value += (byte)diff;

            F.Value = SZ53P(A.Value);
            NF = nf;
            CF = (cf || ((lowNibble < 0x0A) ? (highNibble > 0x09) : (highNibble > 0x08)));
            HF = (nf ? (hf && (lowNibble < 0x06)) : (lowNibble > 0x09));
        }
        private void cpl()
        {
            A.Value ^= 0xFF;

            HF = true;
            NF = true;

            F3 = A.Value.IsBitSet(3);
            F5 = A.Value.IsBitSet(5);
        }
        private void neg()
        {
            byte temp = A.Value;
            A.Value = 0;
            sub(temp);
        }
        private void ccf()
        {
            HF = CF;
            CF = !CF;

            F3 = A.Value.IsBitSet(3);
            F5 = A.Value.IsBitSet(5);

            NF = false;
        }
        private void scf()
        {
            HF = false;
            NF = false;
            CF = true;

            F3 = A.Value.IsBitSet(3);
            F5 = A.Value.IsBitSet(5);
        }

        private void bitHLM(int shift)
        {
            bit(HLM.Value, shift);
            F3 = WZ.Value.IsBitSet(11);
            F5 = WZ.Value.IsBitSet(13);
        }
        private void bit(IRegister<byte> r, int shift) => bit(r.Value, shift);
        private void bit(IRegisterIndexed r, int shift)
        {
            bit(r.Value, shift);

            WZ.Value = r.OffsetAddress;
            F3 = WZ.Value.IsBitSet(11);
            F5 = WZ.Value.IsBitSet(13);
        }
        private void bit(byte val, int shift)
        {
            F.Value &= (R_NF & R_F3 & R_F5 & R_SF);
            
            HF = true;
            ZF = ((val >> shift) & BIT_0_MASK) == 0x00;
            VF = ZF;

            if (NZ && shift == 7)
                F.Value |= S_SF;

            F3 = val.IsBitSet(3);
            F5 = val.IsBitSet(5);
        }

        private void set(IRegister<byte> r, byte bit) => r.Value |= BIT[bit];
        private void res(IRegister<byte> r, byte bit) => r.Value &= NOT[bit];

        private void set(IRegisterIndexed r, byte b) { r.Value |= BIT[b]; WZ.Value = r.OffsetAddress; }
        private void res(IRegisterIndexed r, byte b) { r.Value &= NOT[b]; WZ.Value = r.OffsetAddress; }

        private void add(IRegister<byte> r) => add(r.Value);
        private void add(IRegisterIndexed r)
        {
            add(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void add(byte val)
        {
            int a = A.Value;
            int sum = a + val;

            F.Value = SZ53(sum & 0xFF);
            CF = sum > 0xFF;
            HF = ((a & 0x0F) + (val & 0x0F)) > 0x0F;

            VF = ((a ^ val ^ 0x80) & (val ^ sum) & 0x80) == 0x80;

            A.Value = (byte)sum;
        }
        private void add_n() => add(ByteAtPCPlusCoreOpCodeSize);
        private void add(IRegister<ushort> r1, IRegister<ushort> r2)
        {
            WZ.Value = r1.Value;
            WZ.Inc();

            int sum = r1.Value + r2.Value;

            F.Value = (byte)(F.Value & (S_SF | S_ZF | S_VF));

            HF = ((r1.Value & 0x0FFF) + (r2.Value & 0x0FFF)) > 0x0FFF;

            F5 = ((sum >> 8) & S_F5) == S_F5;
            F3 = ((sum >> 8) & S_F3) == S_F3;
            CF = sum > 0xFFFF;

            r1.Value = (ushort)sum;
        }

        private void adc(IRegister<byte> r) => adc(r.Value);
        private void adc(IRegisterIndexed r)
        {
            adc(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void adc_n() => adc(ByteAtPCPlusCoreOpCodeSize);
        private void adc(byte val)
        {
            int a = A.Value;
            int cfVal = CF ? 1 : 0;
            int sum = a + val + cfVal;

            F.Value = SZ53(sum);

            HF = (a & 0x0F) + (val & 0x0F) + cfVal > 0x0F;

            CF = sum > 0xFF;
            VF = ((a ^ val ^ 0x80) & (val ^ sum) & 0x80) == 0x80;

            A.Value = (byte)sum;
        }
        private void adc_hl(IRegister<ushort> r)
        {
            WZ.Value = HL.Value;
            WZ.Inc();

            int cfVal = CF ? 1 : 0;
            int sum = HL.Value + r.Value + cfVal;

            F.Value = (byte)((sum >> 8) & (S_SF | S_F5 | S_F3));

            HF = ((HL.Value & 0x0FFF) + (r.Value & 0x0FFF) + cfVal) > 0x0FFF;

            VF = ((HL.Value ^ r.Value ^ 0x8000) & (r.Value ^ sum) & 0x8000) == 0x8000;
            ZF = (sum & 0xFFFF) == 0;
            CF = sum > 0xFFFF;

            HL.Value = (ushort)sum;
        }

        private void sub(IRegister<byte> r) => sub(r.Value);
        private void sub(IRegisterIndexed r)
        {
            sub(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void sub_n() => sub(ByteAtPCPlusCoreOpCodeSize);
        private void sub(byte val)
        {
            int a = A.Value;
            int diff = a - val;

            A.Value = (byte)diff;
            F.Value = (byte)(SZ53(diff & 0xFF) | S_NF);

            HF = (a & 0x0F) - (val & 0x0F) < 0;

            CF = diff < 0;
            VF = ((val ^ a) & (a ^ diff) & 0x80) == 0x80;
        }

        private void sbc(IRegister<byte> r) => sbc(r.Value);
        private void sbc(IRegisterIndexed r)
        {
            sbc(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void sbc_n() => sbc(ByteAtPCPlusCoreOpCodeSize);
        private void sbc(byte val)
        {
            int a = A.Value;
            int cfVal = CF ? 1 : 0;
            int diff = a - val - cfVal;

            A.Value = (byte)diff;
            F.Value = (byte)(SZ53(diff) | S_NF);

            HF = (a & 0x0F) - (val & 0x0F) - cfVal < 0;

            CF = diff < 0;
            VF = ((a ^ val) & (a ^ diff) & 0x80) == 0x80;
        }
        private void sbc_hl(IRegister<ushort> r2)
        {
            WZ.Value = HL.Value;
            WZ.Inc();

            int cfVal = CF ? 1 : 0;
            int diff = HL.Value - r2.Value - cfVal;

            F.Value = (byte)(S_NF | (diff >> 8) & (S_SF | S_F5 | S_F3));
            HF = ((HL.Value & 0x0FFF) - (r2.Value & 0x0FFF)) - cfVal < 0;

            VF = ((r2.Value ^ HL.Value) & (HL.Value ^ diff) & 0x8000) != 0x0000;
            ZF = diff == 0;
            CF = diff < 0;

            HL.Value = (ushort)diff;
        }

        private void inc(IRegister<byte> r)
        {
            r.Inc();

            F.Value = (byte)(F53(r.Value) | (F.Value & S_CF));
            Debug.Assert(!NF);
            VF = r.Value == 0x80;
            HF = (r.Value & 0x0F) == 0;
            ZF = r.Value == 0;
            SF = (r.Value & 0x80) == 0x80;
        }
        private void dec(IRegister<byte> r)
        {
            r.Dec();

            F.Value = (byte)(F53(r.Value) | (F.Value & S_CF));
            NF = true;
            SF = (r.Value & 0x80) == 0x80;
            ZF = r.Value == 0;
            HF = (r.Value & 0x0F) == 0x0F;
            VF = r.Value == 0x7F;
        }
        private void inc(IRegister<ushort> r) => r.Inc();
        private void dec(IRegister<ushort> r) => r.Dec();

        private void and(IRegister<byte> r) => and(r.Value);
        private void and(IRegisterIndexed r)
        {
            and(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void and_n() => and(ByteAtPCPlusCoreOpCodeSize);
        private void and(byte b)
        {
            A.Value &= b;
            F.Value = (byte)(SZ53P(A.Value) | S_HF);
        }

        private void or(IRegister<byte> r) => or(r.Value);
        private void or(IRegisterIndexed r)
        {
            or(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void or_n() => or(ByteAtPCPlusCoreOpCodeSize);
        private void or(byte b)
        {
            A.Value |= b;
            F.Value = SZ53P(A.Value);
        }

        private void xor(IRegister<byte> r) => xor(r.Value);
        private void xor(IRegisterIndexed r)
        {
            xor(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void xor_n() => xor(ByteAtPCPlusCoreOpCodeSize);
        private void xor(byte b)
        {
            A.Value ^= b;
            F.Value = SZ53P(A.Value);
        }

        private void cp(IRegister<byte> r) => cp(r.Value);
        private void cp(IRegisterIndexed r)
        {
            cp(r.Value);
            WZ.Value = r.OffsetAddress;
        }
        private void cp_n()
        {
            cp(ByteAtPCPlusCoreOpCodeSize);
        }
        private void cp(byte val)
        {
            int a = A.Value;
            int diff = a - val;

            F.Value = (byte)(SZ(diff & 0xFF) | S_NF);

            HF = (A.Value & 0x0F) - (val & 0x0F) < 0;

            Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));

            VF = ((((a ^ val) & (a ^ diff)) >> 5) & S_VF) == S_VF;
            CF = diff < 0;

            F.Value |= F53(val);
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
            IFF1 = false;
            IFF2 = false;
        }
        private void ei()
        {
            RestoreInterrupts = true;
            restoreInterruptsNow = false;
        }

        private void call()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;

            PushWord(NextPC);
            NextPC = addr;
            WZ.Value = addr;
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
                WZ.Value = WordAtPCPlusInitialOpcodeLength;
            }
        }

        private void ret()
        {
            NextPC = PopWord();
            WZ.Value = NextPC;
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
            IFF1 = IFF2;
            ret();
        }
        private void rst(byte addr)
        {
            PushWord(NextPC);
            NextPC = addr;

            WZ.Value = NextPC;
        }

        private void jp()
        {
            ushort addr = WordAtPCPlusInitialOpcodeLength;

            NextPC = addr;
            WZ.Value = addr;
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
                WZ.Value = WordAtPCPlusInitialOpcodeLength;
            }
        }
        private void jp(IRegister<ushort> r) => NextPC = r.Value;

        private void jr()
        {
            NextPC = PC.Value.Offset(2 + ByteAtPCPlusCoreOpCodeSize.TwosComp());
            WZ.Value = NextPC;
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
            B.Dec();
            if (B.NZ)
            {
                RecordExtraTicks = true;
                NextPC = PC.Value.Offset(2 + ByteAtPCPlusCoreOpCodeSize.TwosComp());
                WZ.Value = NextPC;
            }
        }

        private void rlca()
        {
            A.Value = (byte)((A.Value << 1) | (A.Value >> 7));
            F.Value = (byte)((F.Value & (S_SF | S_ZF | S_VF)) | (A.Value & (S_F5 | S_F3 | S_CF)));
        }
        private void rla()
        {
            byte val = A.Value;
            val <<= 1;

            if (CF)
                val |= 0x01;

            F.Value = (byte)((F.Value & (S_SF | S_ZF | S_VF)) | (val & (S_F5 | S_F3)));
            CF = (A.Value & 0x80) == 0x80;
            A.Value = val;
        }
        private void rrca()
        {
            bool cf = ((A.Value & 0x01) == 0x01);

            A.Value = (byte)((A.Value >> 1) | (A.Value << 7));
            F.Value &= (S_SF | S_ZF | S_VF);

            CF = cf;
            F.Value |= F53(A.Value);
        }
        private void rra()
        {
            int newA = A.Value >> 1;

            if (CF)
                newA |= 0x80;

            F.Value = (byte)(F.Value & (S_SF | S_ZF | S_VF));
            CF = (A.Value & 0x01) == 0x01;
            F.Value |= F53(newA);

            A.Value = (byte)newA;
        }
        private void rlc(IRegister<byte> r)
        {
            int oldVal = r.Value;
            bool cf = ((oldVal & 0x80) == 0x80);
            byte newVal = (byte)((oldVal << 1) | (oldVal >> 7));

            r.Value = newVal;
            F.Value = SZ53P(newVal);
            CF = cf;
        }
        private void rlc(IRegisterIndexed r)
        {
            rlc((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void rl(IRegister<byte> r)
        {
            bool cf = ((r.Value & 0x80) == 0x80);
            int newVal = (r.Value << 1);
            if (CF)
                newVal |= 0x01;
            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = (byte)newVal;
        }
        private void rl(IRegisterIndexed r)
        {
            rl((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void rrc(IRegister<byte> r)
        {
            int oldVal = r.Value;
            bool cf = (oldVal & 0x01) == 0x01;
            byte newVal = (byte)((oldVal >> 1) | (oldVal << 7));

            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = newVal;
        }
        private void rrc(IRegisterIndexed r)
        {
            rrc((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void rr(IRegister<byte> r)
        {
            bool cf = (r.Value & 0x01) == 0x01;
            int newVal = (r.Value >> 1);
            if (CF)
                newVal |= 0x80;
            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = (byte)newVal;
        }
        private void rr(IRegisterIndexed r)
        {
            rr((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void sla(IRegister<byte> r)
        {
            bool cf = (r.Value & 0x80) == 0x80;
            int newVal = (r.Value << 1);
            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = (byte)newVal;
        }
        private void sla(IRegisterIndexed r)
        {
            sla((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void sll(IRegister<byte> r)
        {
            bool cf = (r.Value & 0x80) == 0x80;
            int newVal = (r.Value << 1) | 0x01;
            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = (byte)newVal;
        }
        private void sll(IRegisterIndexed r)
        {
            sll((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void sra(IRegister<byte> r)
        {
            bool cf = (r.Value & 0x01) == 0x01;
            int newVal = ((r.Value >> 1) | (r.Value & 0x80));
            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = (byte)newVal;
        }
        private void sra(IRegisterIndexed r)
        {
            sra((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void srl(IRegister<byte> r)
        {
            bool cf = (r.Value & 0x01) == 0x01;
            int newVal = (r.Value >> 1);
            F.Value = SZ53P(newVal);
            CF = cf;
            r.Value = (byte)newVal;
        }
        private void srl(IRegisterIndexed r)
        {
            srl((IRegister<byte>)r);
            WZ.Value = r.OffsetAddress;
        }
        private void rld()
        {
            int oldHlm = HLM.Value;
            int newHlm = (oldHlm << 4) | (A.Value & 0x0F);

            HLM.Value = (byte)newHlm;
            A.Value = (byte)((A.Value & 0xF0) | (oldHlm >> 4));
            F.Value = (byte)((F.Value & 0x01) | SZ53P(A.Value));

            WZ.Value = HL.Value;
            WZ.Inc();
        }
        private void rrd()
        {
            int oldHlm = HLM.Value;
            int newHlm = (oldHlm >> 4) | (A.Value << 4);

            A.Value = (byte)((A.Value & 0xF0) | (oldHlm & 0x0F));
            HLM.Value = (byte)newHlm;
            F.Value = (byte)((F.Value & 0x01) | SZ53P(A.Value));

            WZ.Value = HL.Value;
            WZ.Inc();
        }

        private void exx()
        {
            ex(BC, BCp);
            ex(DE, DEp);
            ex(HL, HLp);
        }
        private void ex(IRegister<ushort> r1, IRegister<ushort> r2)
        {
            ushort temp = r1.Value;
            r1.Value = r2.Value;
            r2.Value = temp;
        }
        private void ex_spm(IRegister<ushort> r2)
        {
            ushort temp = SPM.Value;
            SPM.Value = r2.Value;
            r2.Value = temp;
            WZ.Value = r2.Value;
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
            WZ.Value = BC.Value;
            WZ.Inc();
            inx(true);
        }
        private void ind()
        {
            WZ.Value = BC.Value;
            WZ.Dec();
            inx(false);
        }
        private void inx(bool IncHL)
        {
            HLM.Value = InPort(C.Value);
            dec(B);
            NF = (HLM.Value & 0x80) != 0x00;

            if (IncHL) HL.Inc(); else HL.Dec();

            // See http://www.z80.info/zip/z80-documented.pdf for weird flag behavior

            byte c = C.Value;

            if (IncHL) c++; else c--;

            int k = c + HLM.Value;
            if (k > 0xFF)
            {
                CF = true;
                HF = true;
            }
            VF = P((k & 0x07) ^ B.Value) != 0;
        }
        private byte InPort(byte pornNum) => ports[pornNum];
        private byte InPortC()
        {
            byte b = InPort(C.Value);
            F.Value = (byte)((F.Value & S_CF) | SZ53P(b));
            return b;
        }
        private void InPortR(IRegister<byte> r) => r.Value = InPortC();
        private void InPortZero() => InPortC();
        private void InPortN()
        {
            byte aVal = A.Value;
            byte portNum = ByteAtPCPlusCoreOpCodeSize;

            A.Value = InPort(portNum);

            portNum++;
            WZ.Value = (ushort)((aVal << 8) | portNum);
        }
        private void InPortA()
        {
            InPortR(A);
            WZ.Value = BC.Value;
            WZ.Inc();
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
            if (B.Value != 0x00)
            {
                NextPC -= 2;
                RecordExtraTicks = true;
            }
        }
        private void outi()
        {
            outx(true);
            WZ.Value = BC.Value;
            WZ.Inc();
        }
        private void outd()
        {
            outx(false);
            WZ.Value = BC.Value;
            WZ.Dec();
        }
        private void outx(bool IncHL)
        {
            OutPort(C.Value, HLM.Value);
            dec(B);

            if (IncHL) HL.Inc(); else HL.Dec();

            // See http://www.z80.info/zip/z80-documented.pdf for weird flag behavior

            int k = L.Value + HLM.Value;
            if (k > 0xFF)
            {
                CF = true;
                HF = true;
            }
            VF = P((k & 0x07) ^ B.Value) != 0;
        }
        private void OutPort(byte pornNum, byte value) => ports[pornNum] = value;
        private void OutPortR(IRegister<byte> r) => OutPort(C.Value, r.Value);
        private void OutPortZero() => OutPort(C.Value, (byte)0);
        private void OutPortN()
        {
            byte aVal = A.Value;
            byte portNum = ByteAtPCPlusCoreOpCodeSize;

            OutPort(portNum, aVal);

            // Note for *BM1: WZ_low = (port + 1) & #FF,  WZ_hi = 0
            portNum++;
            WZ.Value = (ushort)((aVal << 8) | portNum);
        }
        private void OutPortA()
        {
            OutPort(C.Value, A.Value);
            WZ.Value = BC.Value;
            WZ.Inc();
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
