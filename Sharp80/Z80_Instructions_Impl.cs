using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Sharp80.Processor
{
    internal sealed partial class Z80
    {
        private static readonly byte[] BIT = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        private static readonly byte[] NOT = { 0xFE, 0xFD, 0xFB, 0xF7, 0xEF, 0xDF, 0xBF, 0x7F };

        //private void load_a_xxmm(Register8Indirect XX)
        //{
        //    System.Diagnostics.Debug.Assert(XX.Equals(BCM) || XX.Equals(DEM));
        //    A.val = XX.val;
        //    WZ.val = XX.ProxyVal;
        //    WZ.inc();
        //}
        //private void load_xxmm_a(Register8Indirect XX)
        //{
        //    // Note for *BM1: MEMPTR_low = (rp + 1) & #FF,  MEMPTR_hi = 0

        //    System.Diagnostics.Debug.Assert(XX.Equals(BCM) || XX.Equals(DEM));
        //    XX.val = A.val;

        //    WZ.setVal(A.val, (byte)((XX.ProxyVal + 1) & 0xFF));
        //}
        private void load(Register8 r1, Register8 r2)
        {
            r1.val = r2.val;
        }
        private void load(Register8 r1, Register8Indexed r2)
        {
            load(r1, (Register8)r2);
            WZ.val = r2.OffsetAddress;
        }
        private void load(Register8Indexed r1, Register8 r2)
        {
            load((Register8)r1, r2);
            WZ.val = r1.OffsetAddress;
        }
        private void load(Register8 r1, byte b)
        {
            r1.val = b;
        }
        private void load_reg_nn(Register8 r)
        {
            r.val = ByteAtPCPlusInitialOpCodeLength;
        }
        private void load_ixy_nn(Register8Indexed r)
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
            
            WZ.setVal(A.val, (byte)((addr + 1) & 0xFF));

            //Note for *BM1: MEMPTR_low = (addr + 1) & #FF,  MEMPTR_hi = 0
        }
        private void load(Register16 r1, Register16 r2)
        {
            r1.val = r2.val;
        }
        private void load_reg_nnnn(Register16 r)
        {
            r.val = WordAtPCPlusInitialOpcodeLength;
        }
        private void load_ixy_mmmm(Register16 r)
        {
            r.val = Memory.GetWordAt(WordAtPCPlusInitialOpcodeLength);
            // WZ?
        }
        private void load_mmmm_ixy(Register16 r)
        {
            Memory.SetWordAt(WordAtPCPlusInitialOpcodeLength, r.val);
            // WZ?
        }
        private void load_xx_mmmm(Register16 XX)
        {
            var addr = WordAtPCPlusInitialOpcodeLength;
            XX.val = Memory.GetWordAt(addr);
            WZ.val = addr;
            WZ.inc();
        }
        private void load_mmmm_xx(Register16 XX)
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
        private void push(Register16 r)
        {
            PushWord(r.val);
        }
        private void pop(Register16 r)
        {
            r.val = PopWord();
        }
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
            WZ: when BC=1 or A=(HL): exactly as CPI/R
	        In other cases WZ = PC + 1 on each step, where PC = instruction address.
	        Note* since at the last execution BC=1 or A=(HL), resulting MEMPTR = PC + 1 + 1 
	        (if there were not interrupts during the execution) 
            */

            if (BC.NZ && !ZF)
            {
                WZ.val = PC.val;
                WZ.inc();
                RecordExtraTicks = true;
                NextPC -= 2;
                System.Diagnostics.Debug.Assert(this.PC.val == NextPC);
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

            byte hlMem = HLM.val;

            byte diff = (byte)((A.val - hlMem) & 0xFF);
            bool carry = CF;

            F.val = SZ(diff);
            NF = true;
            CF = carry;
            VF = BC.NZ;

            if ((A.val & 0x0F) < (diff & 0x0F))
            {
                HF = true;
                diff--;
            }
            
            System.Diagnostics.Debug.Assert(HF == (((A.val ^ hlMem ^ diff) & S_HF) == S_HF));
           
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

            F.val = (byte)SZ53P(A.val);
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
            F3 = WZ.hVal.IsBitSet(3);
            F5 = WZ.hVal.IsBitSet(5);
        }
        private void bit(Register8 r, int shift)
        {
            bit(r.val, shift);
        }
        private void bit(Register8Indexed r, int shift)
        {
            bit((Register8)r, shift);

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

        private void set(Register8 r, byte bit)
        {
            r.val |= BIT[bit];
        }
        private void res(Register8 r, byte bit)
        {
            r.val &= NOT[bit];
        }

        private void set(Register8Indexed r, byte b) { set((Register8)r, b); WZ.val = r.OffsetAddress; }
        private void res(Register8Indexed r, byte b) { res((Register8)r, b); WZ.val = r.OffsetAddress; }

        private void add(Register8 r)
        {
            add(r.val);
        }
        private void add(Register8Indexed r)
        {
            add((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void add(byte val)
        {
            int a = A.val;
            int sum = a + val;
	
            F.val = SZ53(sum & 0xFF);
            CF = sum > 0xFF;
            //HF = ((a ^ sum ^ val) & S_HF) == S_HF;

            HF = ((a & 0x0F) + (val & 0x0F)) > 0x0F;

            System.Diagnostics.Debug.Assert(HF == (((a ^ sum ^ val) & S_HF) == S_HF));

            VF = ((a ^ val ^ 0x80) & (val ^ sum) & 0x80) == 0x80;

            A.val = (byte)sum;
        }
        private void add_n()
        {
            add(ByteAtPCPlusInitialOpCodeLength);
        }
        private void add(Register16 r1, Register16 r2)
        {
            WZ.val = (ushort)(r1.val + 1);

            int sum = r1.val + r2.val;

    	    F.val = (byte)(F.val & (S_SF | S_ZF | S_VF));

            HF = ((r1.val & 0x0FFF) + (r2.val & 0x0FFF)) > 0x0FFF;

            System.Diagnostics.Debug.Assert(HF == ((((r1.val ^ sum ^ r2.val) >> 8) & S_HF) == S_HF));

            F5 = ((sum >> 8) & S_F5) == S_F5;
            F3 = ((sum >> 8) & S_F3) == S_F3;
            //HF = (((r1.val ^ sum ^ r2.val) >> 8) & S_HF) == S_HF;
            CF = sum > 0xFFFF;
            
            r1.val = (ushort)sum;
        }

        private void adc(Register8 r)
        {
            adc(r.val);
        }
        private void adc(Register8Indexed r)
        {
            adc((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void adc_n()
        {
            adc(ByteAtPCPlusInitialOpCodeLength);
        }
        private void adc(byte val)
        {
            int a = A.val;
            int cfVal = CF ? 1 : 0;
            int sum = a + val + cfVal;

            F.val = SZ53(sum);
            
            //HF = ((a ^ sum ^ val) & S_HF) == S_HF;
            HF = (a & 0x0F) + (val & 0x0F) + cfVal > 0x0F;
            System.Diagnostics.Debug.Assert(HF == (((a ^ sum ^ val) & S_HF) == S_HF));
            
            CF = sum > 0xFF;
            VF = ((a ^ val ^ 0x80) & (val ^ sum) & 0x80) == 0x80;

            A.val = (byte)sum;
        }
        private void adc(Register16 r1, Register16 r2)
        {
            WZ.val = (ushort)(r1.val + 1);

            int cfVal = CF ? 1 : 0;
            int sum = r1.val + r2.val + cfVal;

            F.val = (byte)((sum >> 8) & (S_SF | S_F5 | S_F3));

            //HF = (((r1.val ^ sum ^ r2.val) >> 8) & S_HF) != 0;
            HF = ((r1.val & 0x0FFF) + (r2.val & 0x0FFF) + cfVal) > 0x0FFF;
            System.Diagnostics.Debug.Assert(HF == ((((r1.val ^ sum ^ r2.val) >> 8) & S_HF) != 0));

            VF = ((r1.val ^ r2.val ^ 0x8000) & (r2.val ^ sum) & 0x8000) == 0x8000;
            ZF = (sum & 0xFFFF) == 0;
            CF = sum > 0xFFFF;

            r1.val = (ushort)sum;
        }

        private void sub(Register8 r)
        {
            sub(r.val);
        }
        private void sub(Register8Indexed r)
        {
            sub((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void sub_n()
        {
            sub(ByteAtPCPlusInitialOpCodeLength);
        }
        private void sbc(Register8 r)
        {
            sbc(r.val);
        }
        private void sbc(Register8Indexed r)
        {
            sbc((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void sbc_n()
        {
            sbc(ByteAtPCPlusInitialOpCodeLength);
        }
        private void sub(byte val)
        {
            int a = A.val;
            int diff = a - val;

            A.val = (byte)diff;
            F.val = (byte)(SZ53(diff & 0xFF) | S_NF);

            HF = (a & 0x0F) - (val & 0x0F) < 0;

            System.Diagnostics.Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));
            
            CF = diff < 0;
            VF = ((val ^ a) & (a ^ diff) & 0x80) == 0x80;
        }
        private void sbc(byte val)
        {
            int a = A.val;
            int cfVal = CF ? 1 : 0;
            int diff = a - val - cfVal;

            A.val = (byte)diff;
	        F.val = (byte)(SZ53(diff) | S_NF);

            HF = (a & 0x0F) - (val & 0x0F) - cfVal < 0;
            
            System.Diagnostics.Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));

            CF = diff < 0;
            VF = ((a ^ val) & (a ^ diff) & 0x80) == 0x80;
        }
        private void sbc(Register16 r1, Register16 r2)
        {
            Debug.Assert(r1.Equals(HL));

            WZ.val = (ushort)(r1.val + 1);

            int cfVal = CF ? 1 : 0;

            int diff = r1.val - r2.val - cfVal;
       
            F.val = (byte)(S_NF | (diff >> 8) & (S_SF | S_F5 | S_F3));

            //HF = (((r1.val ^ diff ^ r2.val) >> 8) & S_HF) != 0x00;

            HF = ((r1.val & 0x0FFF) - (r2.val & 0x0FFF)) - cfVal < 0;

            System.Diagnostics.Debug.Assert(HF == ((((r1.val ^ diff ^ r2.val) >> 8) & S_HF) != 0x00));

            VF = ((r2.val ^ r1.val) & (r1.val ^ diff) & 0x8000) != 0x0000;
            ZF = diff == 0;
            CF = diff < 0;

            r1.val = (ushort)(diff & 0xFFFF);
        }
        private void inc(Register8 r)
        {

            r.inc();
#if true
            F.val = (byte)(F53(r.val) | (F.val & S_CF));
            System.Diagnostics.Debug.Assert(!NF);
            VF = r.val == 0x80;
            HF = (r.val & 0x0F) == 0;
            ZF = r.val == 0;
            SF = (r.val & 0x80) == 0x80;
#else
            F.val = (byte)((F.val & S_CF) | SZ53(r.val));
            HF = (r.val & 0x0F) == 0x00;
            VF = r.val == 0x80;
#endif
        }
        private void dec(Register8 r)
        {
            r.dec();

            F.val = (byte)(F53(r.val) | (F.val & S_CF));
            NF = true;
            SF = (r.val & 0x80) == 0x80;
            ZF = r.val == 0;
            HF = (r.val & 0x0F) == 0x0F;
            VF = r.val == 0x7F;
        }
        private void inc(Register16 r)
        {
            r.inc();
        }
        private void dec(Register16 r)
        {
            r.dec();
        }

        private void and(Register8 r)
        {
            and(r.val);
        }
        private void and(Register8Indexed r)
        {
            and((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void and_n()
        {
            and(ByteAtPCPlusInitialOpCodeLength);
        }
        private void and(byte b)
        {
            A.val &= b;
            F.val = (byte)(SZ53P(A.val) | S_HF);
        }
        
        private void or(Register8 r)
        {
            or(r.val);
        }
        private void or(Register8Indexed r)
        {
            or((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void or_n()
        {
            or(ByteAtPCPlusInitialOpCodeLength);
        }
        private void or(byte b)
        {
            A.val |= b;
            F.val = SZ53P(A.val);
        }
        private void xor(Register8 r)
        {
            xor(r.val);
        }
        private void xor(Register8Indexed r)
        {
            xor((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void xor_n()
        {
            xor(ByteAtPCPlusInitialOpCodeLength);
        }
        private void xor(byte b)
        {
            A.val ^= b;
            F.val = SZ53P(A.val);
        }
        private void cp(Register8 r)
        {
            cp(r.val);
        }
        private void cp(Register8Indexed r)
        {
            cp((Register8)r);
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

            System.Diagnostics.Debug.Assert(HF == (((a ^ diff ^ val) & S_HF) == S_HF));

            VF = ((((a ^ val) & (a ^ diff)) >> 5) & S_VF) == S_VF;
            CF = diff < 0;

            F.val |= F53(val);
        }

        private void im(int mode)
        {
            switch (mode)
            {
                case 0:
                    interruptMode = 0;
                    break;
                case 1:
                    interruptMode = 1;
                    break;
                case 2:
                    interruptMode = 2;
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
            NextPC = (ushort)PopWord();
            WZ.val = NextPC;
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

            if (Log.DebugOn)
                Log.LogToDebug(string.Format("Return from Interrupt, IFF1 {0} -> {1}", IFF1, IFF2));

            IFF1 = IFF2;
            ret();
        }
        private void rst(byte addr)
        {
            PushWord(NextPC);
            NextPC = (ushort)addr;
            
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
        private void jp(Register16Compound r)
        {
            NextPC = r.val;
        }
        private void jr()
        {
            NextPC = (ushort)((PC.val + 2 + Lib.TwosComp(ByteAtPCPlusInitialOpCodeLength)) & 0xFFFF);

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
                NextPC = (ushort)((PC.val + 2 + Lib.TwosComp(ByteAtPCPlusInitialOpCodeLength)) & 0xFFFF);
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
        private void rlc(Register8 r)
        {
            int oldVal = r.val;
            bool cf = ((oldVal & 0x80) == 0x80);
            byte newVal = (byte)((oldVal << 1) | (oldVal >> 7));

            r.val = newVal;
            F.val = SZ53P(newVal);
            CF = cf;
        }
        private void rlc(Register8Indexed r)
        {
            rlc((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void rl(Register8 r)
        {
            bool cf = ((r.val & 0x80) == 0x80);
            int newVal = (r.val << 1);
            if (CF)
                newVal |= 0x01;
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void rl(Register8Indexed r)
        {
            rl((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void rrc(Register8 r)
        {
            int oldVal = r.val;
            bool cf = (oldVal & 0x01) == 0x01;
            byte newVal = (byte)((oldVal >> 1) | (oldVal << 7));
            
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = newVal;
        }
        private void rrc(Register8Indexed r)
        {
            rrc((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void rr(Register8 r)
        {
            bool cf = (r.val & 0x01) == 0x01;
            int newVal = (r.val >> 1);
            if (CF)
                newVal |= 0x80;
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void rr(Register8Indexed r)
        {
            rr((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void sla(Register8 r)
        {
            bool cf = (r.val & 0x80) == 0x80;
            int newVal = (r.val << 1);
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void sla(Register8Indexed r)
        {
            sla((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void sll(Register8 r)
        {
            bool cf = (r.val & 0x80) == 0x80;
            int newVal = (r.val << 1) | 0x01;
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void sll(Register8Indexed r)
        {
            sll((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void sra(Register8 r)
        {
            bool cf = (r.val & 0x01) == 0x01;
            int newVal = ((r.val >> 1) | (r.val & 0x80));
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void sra(Register8Indexed r)
        {
            sra((Register8)r);
            WZ.val = r.OffsetAddress;
        }
        private void srl(Register8 r)
        {
            bool cf = (r.val & 0x01) == 0x01;
            int newVal = (r.val >> 1);
            F.val = SZ53P(newVal);
            CF = cf;
            r.val = (byte)newVal;
        }
        private void srl(Register8Indexed r)
        {
            srl((Register8)r);
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
        private void ex(Register16Compound r1, Register16Compound r2)
        {
            ushort temp = r1.val;
            r1.val = r2.val;
            r2.val = temp;
        }
        private void ex_spm(Register16Compound r2)
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
        private void rlc_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); rlc(r2); load(r1, r2);
        }
        private void rrc_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); rrc(r2); load(r1, r2);
        }
        private void rl_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); rl(r2); load(r1, r2);
        }
        private void rr_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); rr(r2); load(r1, r2);
        }
        private void sla_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); sla(r2); load(r1, r2);
        }
        private void sra_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); sra(r2); load(r1, r2);
        }
        private void sll_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); sll(r2); load(r1, r2);
        }
        private void srl_compound(Register8Indexed r1, Register8 r2)
        {
            load(r2, r1); srl(r2); load(r1, r2);
        }
        private void res_compound(Register8Indexed r1, Register8 r2, byte bit)
        {
            load(r2, r1); res(r2, bit); load(r1, r2);
        }
        private void set_compound(Register8Indexed r1, Register8 r2, byte bit)
        {
            load(r2, r1); set(r2, bit); load(r1, r2);
        }

    }
}
