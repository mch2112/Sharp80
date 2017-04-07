/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details. 

using System;

namespace Sharp80.Z80
{
    public partial class Z80 : IStatus
    {
        private readonly InstructionSet instructionSet;

        private void SetupInstructionObjects()
        {
            IRegister<byte>[] r8 = new IRegister<byte>[] { B, C, D, E, H, L, HLM, A };
            IRegister<ushort>[] r16s = new IRegister<ushort>[] { BC, DE, HL, SP };
            IRegister<ushort>[] r16a = new IRegister<ushort>[] { BC, DE, HL, AF };
            IRegister<ushort>[] r16xs = new IRegister<ushort>[] { BC, DE, IX, SP };
            IRegister<ushort>[] r16ys = new IRegister<ushort>[] { BC, DE, IY, SP };
            IRegisterIndexed[] rxyi = new IRegisterIndexed[] { IXM, IYM };
            IRegister<ushort>[] rxy = new IRegister<ushort>[] { IX, IY };

            IRegister<byte>[] r8x = new IRegister<byte>[] { B, C, D, E, IX.H, IX.L, null, A };
            IRegister<byte>[] r8y = new IRegister<byte>[] { B, C, D, E, IY.H, IY.L, null, A };

            byte[] iter8 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }; // prevent closure capture by using foreach instead of for
            byte[] iter4 = new byte[] { 0, 1, 2, 3 };
            byte[] iter2 = new byte[] { 0, 1 };
            byte[] iter8alt = new byte[] { 0, 1, 2, 3, 4, 5, 7 }; // skips HLM entry

            byte HLMIndex = 6;
            byte opCode;
            byte tStates;

            instructionSet.Add(new Instruction("ADC N", 7, adc_n, 0xCE));
            instructionSet.Add(new Instruction("ADD N", 7, add_n, 0xC6));
            instructionSet.Add(new Instruction("SUB N", 7, sub_n, 0xD6));
            instructionSet.Add(new Instruction("SBC N", 7, sbc_n, 0xDE));
            instructionSet.Add(new Instruction("AND N", 7, and_n, 0xE6));
            instructionSet.Add(new Instruction("OR N", 7, or_n, 0xF6));
            instructionSet.Add(new Instruction("XOR N", 7, xor_n, 0xEE));
            instructionSet.Add(new Instruction("CP N", 7, cp_n, 0xFE));

            instructionSet.Add(new Instruction("LDI", 16, ldi, 0xED, 0xA0));
            instructionSet.Add(new Instruction("LDIR", 16, ldir, 0xED, 0xB0).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("LDD", 16, ldd, 0xED, 0xA8));
            instructionSet.Add(new Instruction("LDDR", 16, lddr, 0xED, 0xB8).WithTStatesAlt(5));

            instructionSet.Add(new Instruction("CPI", 16, cpi, 0xED, 0xA1));
            instructionSet.Add(new Instruction("CPIR", 16, cpir, 0xED, 0xB1).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("CPD", 16, cpd, 0xED, 0xA9));
            instructionSet.Add(new Instruction("CPDR", 16, cpdr, 0xED, 0xB9).WithTStatesAlt(5));

            instructionSet.Add(new Instruction("NOP", 4, nop, 0x00));
            instructionSet.Add(new Instruction("DAA", 4, daa, 0x27));
            instructionSet.Add(new Instruction("CPL", 4, cpl, 0x2F));
            instructionSet.Add(new Instruction("CCF", 4, ccf, 0x3F));
            instructionSet.Add(new Instruction("SCF", 4, scf, 0x37));
            instructionSet.Add(new Instruction("HALT", 6, halt, 0x76));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x44));

            instructionSet.Add(new Instruction("DI", 4, di, 0xF3));
            instructionSet.Add(new Instruction("EI", 4, ei, 0xFB));

            instructionSet.Add(new Instruction("IM 0", 8, () => im(0), 0xED, 0x46));
            instructionSet.Add(new Instruction("IM 1", 8, () => im(1), 0xED, 0x56));
            instructionSet.Add(new Instruction("IM 2", 8, () => im(2), 0xED, 0x5E));

            instructionSet.Add(new Instruction("RLCA", 4, rlca, 0x07));
            instructionSet.Add(new Instruction("RLA", 4, rla, 0x17));
            instructionSet.Add(new Instruction("RRCA", 4, rrca, 0x0F));
            instructionSet.Add(new Instruction("RRA", 4, rra, 0x1F));

            instructionSet.Add(new Instruction("RLD", 18, rld, 0xED, 0x6F));
            instructionSet.Add(new Instruction("RRD", 18, rrd, 0xED, 0x67));

            instructionSet.Add(new Instruction("IN A, (N)", 11, InPortN, 0xDB));
            instructionSet.Add(new Instruction("IN (C)", 12, InPortZero, 0xED, 0x70));
            instructionSet.Add(new Instruction("OUT (C), 0", 12, OutPortZero, 0xED, 0x71));

            foreach (var i in iter8)
            {
                tStates = (byte)(i == HLMIndex ? 7 : 4); // (HL) takes longer

                instructionSet.Add(new Instruction($"ADD {r8[i].Name}", tStates, () => add(r8[i]), (byte)(0x80 + i)));
                instructionSet.Add(new Instruction($"ADC {r8[i].Name}", tStates, () => adc(r8[i]), (byte)(0x88 + i)));
                instructionSet.Add(new Instruction($"SUB {r8[i].Name}", tStates, () => sub(r8[i]), (byte)(0x90 + i)));
                instructionSet.Add(new Instruction($"SBC {r8[i].Name}", tStates, () => sbc(r8[i]), (byte)(0x98 + i)));
                instructionSet.Add(new Instruction($"AND {r8[i].Name}", tStates, () => and(r8[i]), (byte)(0xA0 + i)));
                instructionSet.Add(new Instruction($"XOR {r8[i].Name}", tStates, () => xor(r8[i]), (byte)(0xA8 + i)));
                instructionSet.Add(new Instruction($"OR {r8[i].Name}", tStates, () => or(r8[i]), (byte)(0xB0 + i)));
                instructionSet.Add(new Instruction($"CP {r8[i].Name}", tStates, () => cp(r8[i]), (byte)(0xB8 + i)));

                tStates = (byte)(i == HLMIndex ? 11 : 4);

                instructionSet.Add(new Instruction($"INC {r8[i].Name}", tStates, () => inc(r8[i]), (byte)(0x04 + i * 0x08)));
                instructionSet.Add(new Instruction($"DEC {r8[i].Name}", tStates, () => dec(r8[i]), (byte)(0x05 + i * 0x08)));

                tStates = (byte)(i == HLMIndex ? 15 : 8);

                instructionSet.Add(new Instruction($"RLC {r8[i].Name}", tStates, () => rlc(r8[i]), 0xCB, i));
                instructionSet.Add(new Instruction($"RRC {r8[i].Name}", tStates, () => rrc(r8[i]), 0xCB, (byte)(i + 0x08)));
                instructionSet.Add(new Instruction($"RL {r8[i].Name}", tStates, () => rl(r8[i]), 0xCB, (byte)(i + 0x10)));
                instructionSet.Add(new Instruction($"RR {r8[i].Name}", tStates, () => rr(r8[i]), 0xCB, (byte)(i + 0x18)));
                instructionSet.Add(new Instruction($"SLA {r8[i].Name}", tStates, () => sla(r8[i]), 0xCB, (byte)(i + 0x20)));
                instructionSet.Add(new Instruction($"SRA {r8[i].Name}", tStates, () => sra(r8[i]), 0xCB, (byte)(i + 0x28)));
                instructionSet.Add(new Instruction($"SLL {r8[i].Name}", tStates, () => sll(r8[i]), 0xCB, (byte)(i + 0x30)));
                instructionSet.Add(new Instruction($"SRL {r8[i].Name}", tStates, () => srl(r8[i]), 0xCB, (byte)(i + 0x38)));

                tStates = (byte)(i == HLMIndex ? 10 : 7);

                instructionSet.Add(new Instruction($"LD {r8[i].Name}, N", tStates, () => load_reg_nn(r8[i]), (byte)(i * 0x08 + 0x06)));

                if (i != HLMIndex)
                {
                    foreach (var j in iter2)
                    {
                        instructionSet.Add(new Instruction($"LD {r8[i].Name}, ({rxyi[j].Proxy.Name}+d)", 19, () => load(r8[i], rxyi[j]), (byte)(0xDD + 0x20 * j), (byte)(0x46 + i * 0x08)));
                        instructionSet.Add(new Instruction($"LD ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 19, () => load(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), (byte)(0x70 + i)));

                        // COMPOUND INSTRUCTIONS (UNDOCUMENTED)
                        instructionSet.Add(new Instruction($"RLC ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => rlc_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, i));
                        instructionSet.Add(new Instruction($"RRC ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => rrc_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x08 + i)));
                        instructionSet.Add(new Instruction($"RL ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => rl_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x10 + i)));
                        instructionSet.Add(new Instruction($"RR ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => rr_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x18 + i)));
                        instructionSet.Add(new Instruction($"SLA ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => sla_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x20 + i)));
                        instructionSet.Add(new Instruction($"SRA ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => sra_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x28 + i)));
                        instructionSet.Add(new Instruction($"SLL ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => sll_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x30 + i)));
                        instructionSet.Add(new Instruction($"SRL ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", 23, () => srl_compound(rxyi[j], r8[i]), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x38 + i)));
                        foreach (var k in iter8)
                        {
                            instructionSet.Add(new Instruction(string.Format("RES {0}, ({1}+d), {2}", k, rxyi[j].Proxy.Name, r8[i].Name), 23, () => res_compound(rxyi[j], r8[i], k), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x80 + k * 0x08 + i)));
                            instructionSet.Add(new Instruction(string.Format("SET {0}, ({1}+d), {2}", k, rxyi[j].Proxy.Name, r8[i].Name), 23, () => set_compound(rxyi[j], r8[i], k), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0xC0 + k * 0x08 + i)));
                        }
                    }
                    instructionSet.Add(new Instruction($"LD {r8x[i].Name}, IXh", 8, () => load(r8x[i], IX.H), 0xDD, (byte)(0x44 + i * 0x08)));
                    instructionSet.Add(new Instruction($"LD {r8x[i].Name}, IXl", 8, () => load(r8x[i], IX.L), 0xDD, (byte)(0x45 + i * 0x08)));

                    instructionSet.Add(new Instruction($"LD {r8y[i].Name}, IYh", 8, () => load(r8y[i], IY.H), 0xFD, (byte)(0x44 + i * 0x08)));
                    instructionSet.Add(new Instruction($"LD {r8y[i].Name}, IYl", 8, () => load(r8y[i], IY.L), 0xFD, (byte)(0x45 + i * 0x08)));

                    if (i <= 3 || i == 7) // r is B, C, D, E, or A
                    {
                        instructionSet.Add(new Instruction($"LD IXh, {r8x[i].Name}", 8, () => load(IX.H, r8x[i]), 0xDD, (byte)(0x60 + i)));
                        instructionSet.Add(new Instruction($"LD IXl, {r8x[i].Name}", 8, () => load(IX.L, r8x[i]), 0xDD, (byte)(0x68 + i)));

                        instructionSet.Add(new Instruction($"LD IYh, {r8y[i].Name}", 8, () => load(IY.H, r8y[i]), 0xFD, (byte)(0x60 + i)));
                        instructionSet.Add(new Instruction($"LD IYl, {r8y[i].Name}", 8, () => load(IY.L, r8y[i]), 0xFD, (byte)(0x68 + i)));
                    }

                    instructionSet.Add(new Instruction($"IN {r8[i].Name}, (C)", 12, () => InPortR(r8[i]), 0xED, (byte)(0x40 + i * 0x08)));
                    instructionSet.Add(new Instruction($"OUT (C), {r8[i].Name}", 12, () => OutPortR(r8[i]), 0xED, (byte)(0x41 + i * 0x08)));
                }

                foreach (var j in iter2)
                {
                    instructionSet.Add(new Instruction($"SET {i}, ({rxyi[j].Proxy.Name}+d)", 23, () => set(rxyi[j], i), (byte)(0xDD + j * 0x20), 0xCB, (byte)(0xC6 + i * 0x08)));
                    instructionSet.Add(new Instruction($"RES {i}, ({rxyi[j].Proxy.Name}+d)", 23, () => res(rxyi[j], i), (byte)(0xDD + j * 0x20), 0xCB, (byte)(0x86 + i * 0x08)));
                }

                foreach (var j in iter8)
                {
                    tStates = (byte)(i == HLMIndex || j == HLMIndex ? 7 : 4); // (HL) takes longer
                    opCode = (byte)(0x40 + 0x08 * i + j);
                    if (opCode != 0x76) // halt
                        instructionSet.Add(new Instruction($"LD {r8[i].Name}, {r8[j].Name}", tStates, () => load(r8[i], r8[j]), opCode));

                    tStates = (byte)(i == HLMIndex ? 12 : 8);

                    instructionSet.Add(new Instruction($"BIT {j}, {r8[i].Name}", tStates, i == HLMIndex ? new Action(() => bitHLM(j))
                                      : new Action(() => bit(r8[i], j)), 0xCB,
                        (byte)(0x40 + j * 0x08 + i)));

                    instructionSet.Add(new Instruction($"SET {j}, {r8[i].Name}", tStates, () => set(r8[i], j), 0xCB, (byte)(0xC0 + j * 0x08 + i)));
                    instructionSet.Add(new Instruction($"RES {j}, {r8[i].Name}", tStates, () => res(r8[i], j), 0xCB, (byte)(0x80 + j * 0x08 + i)));

                    foreach (var k in iter2)
                    {
                        // redundant opcodes
                        instructionSet.Add(new Instruction($"BIT {i}, ({rxyi[k].Proxy.Name}+d)", 20, () => bit(rxyi[k], i), (byte)(0xDD + k * 0x20), 0xCB, (byte)(0x40 + i * 8 + j)));
                    }
                }
            }

            foreach (var i in iter4)
            {
                instructionSet.Add(new Instruction(string.Format("LD {0}, NN", r16s[i].Name), 10, () => load_reg_nnnn(r16s[i]), (byte)(0x01 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("LD {0}, (NN)", r16s[i].Name), 20, () => load_xx_mmmm(r16s[i]), 0xED, (byte)(0x4B + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("LD (NN), {0}", r16s[i].Name), 20, () => load_mmmm_xx(r16s[i]), 0xED, (byte)(0x43 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("INC {0}", r16s[i].Name), 6, () => inc(r16s[i]), (byte)(0x03 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("DEC {0}", r16s[i].Name), 6, () => dec(r16s[i]), (byte)(0x0B + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("ADD HL, {0}", r16s[i].Name), 11, () => add(HL, r16s[i]), (byte)(0x09 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("SBC HL, {0}", r16s[i].Name), 15, () => sbc(HL, r16s[i]), 0xED, (byte)(0x42 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("ADC HL, {0}", r16s[i].Name), 15, () => adc(HL, r16s[i]), 0xED, (byte)(0x4A + i * 0x10)));

                instructionSet.Add(new Instruction(string.Format("POP {0}", r16a[i].Name), 10, () => pop(r16a[i]), (byte)(0xC1 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("PUSH {0}", r16a[i].Name), 11, () => push(r16a[i]), (byte)(0xC5 + i * 0x10)));

                instructionSet.Add(new Instruction(string.Format("ADD {0}, {1}", IX.Name, r16xs[i].Name), 15, () => add(IX, r16xs[i]), 0xDD, (byte)(0x09 + i * 0x10)));
                instructionSet.Add(new Instruction(string.Format("ADD {0}, {1}", IY.Name, r16ys[i].Name), 15, () => add(IY, r16ys[i]), 0xFD, (byte)(0x09 + i * 0x10)));
            }
            foreach (var i in iter2)
            {
                instructionSet.Add(new Instruction(string.Format("LD ({0}+d), N", rxyi[i].Proxy.Name), 10, () => load_ixy_nn(rxyi[i]), (byte)(0xDD + i * 0x20), 0x36));
                instructionSet.Add(new Instruction(string.Format("LD {0}, NN", rxy[i].Name), 14, () => load_reg_nnnn(rxy[i]), (byte)(0xDD + i * 0x20), 0x21));
                instructionSet.Add(new Instruction(string.Format("LD (NN), {0}", rxy[i].Name), 20, () => load_mmmm_ixy(rxy[i]), (byte)(0xDD + i * 0x20), 0x22));
                instructionSet.Add(new Instruction(string.Format("LD {0}, (NN)", rxy[i].Name), 20, () => load_ixy_mmmm(rxy[i]), (byte)(0xDD + i * 0x20), 0x2A));
                instructionSet.Add(new Instruction(string.Format("INC {0}", rxy[i].Name), 10, () => inc(rxy[i]), (byte)(0xDD + i * 0x20), 0x23));
                instructionSet.Add(new Instruction(string.Format("DEC {0}", rxy[i].Name), 10, () => dec(rxy[i]), (byte)(0xDD + i * 0x20), 0x2B));
                instructionSet.Add(new Instruction(string.Format("POP {0}", rxy[i].Name), 14, () => pop(rxy[i]), (byte)(0xDD + i * 0x20), 0xE1));
                instructionSet.Add(new Instruction(string.Format("PUSH {0}", rxy[i].Name), 15, () => push(rxy[i]), (byte)(0xDD + i * 0x20), 0xE5));

                instructionSet.Add(new Instruction(string.Format("INC ({0}+d)", rxyi[i].Proxy.Name), 23, () => inc(rxyi[i]), (byte)(0xDD + i * 0x20), 0x34));
                instructionSet.Add(new Instruction(string.Format("DEC ({0}+d)", rxyi[i].Proxy.Name), 23, () => dec(rxyi[i]), (byte)(0xDD + i * 0x20), 0x35));

                instructionSet.Add(new Instruction(string.Format("ADD ({0}+d)", rxyi[i].Proxy.Name), 19, () => add(rxyi[i]), (byte)(0xDD + i * 0x20), 0x86));
                instructionSet.Add(new Instruction(string.Format("ADC ({0}+d)", rxyi[i].Proxy.Name), 19, () => adc(rxyi[i]), (byte)(0xDD + i * 0x20), 0x8E));
                instructionSet.Add(new Instruction(string.Format("SUB ({0}+d)", rxyi[i].Proxy.Name), 19, () => sub(rxyi[i]), (byte)(0xDD + i * 0x20), 0x96));
                instructionSet.Add(new Instruction(string.Format("SBC ({0}+d)", rxyi[i].Proxy.Name), 19, () => sbc(rxyi[i]), (byte)(0xDD + i * 0x20), 0x9E));

                instructionSet.Add(new Instruction(string.Format("AND ({0}+d)", rxyi[i].Proxy.Name), 19, () => and(rxyi[i]), (byte)(0xDD + i * 0x20), 0xA6));
                instructionSet.Add(new Instruction(string.Format("XOR ({0}+d)", rxyi[i].Proxy.Name), 19, () => xor(rxyi[i]), (byte)(0xDD + i * 0x20), 0xAE));
                instructionSet.Add(new Instruction(string.Format("OR ({0}+d)", rxyi[i].Proxy.Name), 19, () => or(rxyi[i]), (byte)(0xDD + i * 0x20), 0xB6));
                instructionSet.Add(new Instruction(string.Format("CP ({0}+d)", rxyi[i].Proxy.Name), 19, () => cp(rxyi[i]), (byte)(0xDD + i * 0x20), 0xBE));

                instructionSet.Add(new Instruction(string.Format("RLC ({0}+d)", rxyi[i].Proxy.Name), 23, () => rlc(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x06));
                instructionSet.Add(new Instruction(string.Format("RRC ({0}+d)", rxyi[i].Proxy.Name), 23, () => rrc(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x0E));
                instructionSet.Add(new Instruction(string.Format("RL ({0}+d)", rxyi[i].Proxy.Name),  23, () => rl(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x16));
                instructionSet.Add(new Instruction(string.Format("RR ({0}+d)", rxyi[i].Proxy.Name),  23, () => rr(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x1E));
                instructionSet.Add(new Instruction(string.Format("SLA ({0}+d)", rxyi[i].Proxy.Name), 23, () => sla(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x26));
                instructionSet.Add(new Instruction(string.Format("SRA ({0}+d)", rxyi[i].Proxy.Name), 23, () => sra(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x2E));
                instructionSet.Add(new Instruction(string.Format("SLL ({0}+d)", rxyi[i].Proxy.Name), 23, () => sll(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x36));
                instructionSet.Add(new Instruction(string.Format("SRL ({0}+d)", rxyi[i].Proxy.Name), 23, () => srl(rxyi[i]), (byte)(0xDD + i * 0x20), 0xCB, 0x3E));
            }

            instructionSet.Add(new Instruction("LD A, (BC)", 7, () => load(A, BCM), 0x0A));
            instructionSet.Add(new Instruction("LD A, (DE)", 7, () => load(A, DEM), 0x1A));
            instructionSet.Add(new Instruction("LD A, (NN)", 13, load_a_mmmm, 0x3A));

            instructionSet.Add(new Instruction("LD (BC), A", 7, () => load(BCM, A), 0x02));
            instructionSet.Add(new Instruction("LD (DE), A", 7, () => load(DEM, A), 0x12));
            instructionSet.Add(new Instruction("LD (NN), A", 13, load_mmmm_a, 0x32));

            instructionSet.Add(new Instruction("LD A, I", 9, load_a_i, 0xED, 0x57));
            instructionSet.Add(new Instruction("LD A, R", 9, load_a_r, 0xED, 0x5F));
            instructionSet.Add(new Instruction("LD I, A", 9, () => load(I, A), 0xED, 0x47));
            instructionSet.Add(new Instruction("LD R, A", 9, () => load(R, A), 0xED, 0x4F));

            instructionSet.Add(new Instruction("LD HL, (NN)", 16, () => load_xx_mmmm(HL), 0x2A));
            instructionSet.Add(new Instruction("LD (NN), HL", 16, () => load_mmmm_xx(HL), 0x22));

            instructionSet.Add(new Instruction("LD SP, HL", 6, () => load(SP, HL), 0xF9));
            instructionSet.Add(new Instruction("LD SP, IX", 10, () => load(SP, IX), 0xDD, 0xF9));
            instructionSet.Add(new Instruction("LD SP, IY", 10, () => load(SP, IY), 0xFD, 0xF9));

            instructionSet.Add(new Instruction("LD IXh, N", 11, () => load_reg_nn(IX.H), 0xDD, 0x26));
            instructionSet.Add(new Instruction("LD IYh, N", 11, () => load_reg_nn(IY.H), 0xFD, 0x26));
            instructionSet.Add(new Instruction("LD IXl, N", 11, () => load_reg_nn(IX.L), 0xDD, 0x2E));
            instructionSet.Add(new Instruction("LD IYl, N", 11, () => load_reg_nn(IY.L), 0xFD, 0x2E));

            instructionSet.Add(new Instruction("EXX", 4, exx, 0xD9));
            instructionSet.Add(new Instruction("EX DE, HL", 4, () => ex(DE, HL), 0xEB));
            instructionSet.Add(new Instruction("EX AF, AF'", 4, () => ex(AF, AFp), 0x08));
            instructionSet.Add(new Instruction("EX (SP), HL", 19, () => ex_spm(HL), 0xE3));
            instructionSet.Add(new Instruction("EX (SP), IX", 23, () => ex_spm(IX), 0xDD, 0xE3));
            instructionSet.Add(new Instruction("EX (SP), IY", 23, () => ex_spm(IY), 0xFD, 0xE3));

            instructionSet.Add(new Instruction("ADD IXh", 19, () => add(IX.H), 0xDD, 0x84));
            instructionSet.Add(new Instruction("ADD IYh", 19, () => add(IY.H), 0xFD, 0x84));
            instructionSet.Add(new Instruction("ADD IXl", 19, () => add(IX.L), 0xDD, 0x85));
            instructionSet.Add(new Instruction("ADD IYl", 19, () => add(IY.L), 0xFD, 0x85));

            instructionSet.Add(new Instruction("ADC IXh", 19, () => adc(IX.H), 0xDD, 0x8C));
            instructionSet.Add(new Instruction("ADC IYh", 19, () => adc(IY.H), 0xFD, 0x8C));
            instructionSet.Add(new Instruction("ADC IXl", 19, () => adc(IX.L), 0xDD, 0x8D));
            instructionSet.Add(new Instruction("ADC IYl", 19, () => adc(IY.L), 0xFD, 0x8D));

            instructionSet.Add(new Instruction("SUB IXh", 19, () => sub(IX.H), 0xDD, 0x94));
            instructionSet.Add(new Instruction("SUB IYh", 19, () => sub(IY.H), 0xFD, 0x94));
            instructionSet.Add(new Instruction("SUB IXl", 19, () => sub(IX.L), 0xDD, 0x95));
            instructionSet.Add(new Instruction("SUB IYl", 19, () => sub(IY.L), 0xFD, 0x95));

            instructionSet.Add(new Instruction("SBC IXh", 19, () => sbc(IX.H), 0xDD, 0x9C));
            instructionSet.Add(new Instruction("SBC IYh", 19, () => sbc(IY.H), 0xFD, 0x9C));
            instructionSet.Add(new Instruction("SBC IXl", 19, () => sbc(IX.L), 0xDD, 0x9D));
            instructionSet.Add(new Instruction("SBC IYl", 19, () => sbc(IY.L), 0xFD, 0x9D));

            instructionSet.Add(new Instruction("AND IXh", 19, () => and(IX.H), 0xDD, 0xA4));
            instructionSet.Add(new Instruction("AND IYh", 19, () => and(IY.H), 0xFD, 0xA4));
            instructionSet.Add(new Instruction("AND IXl", 19, () => and(IX.L), 0xDD, 0xA5));
            instructionSet.Add(new Instruction("AND IYl", 19, () => and(IY.L), 0xFD, 0xA5));

            instructionSet.Add(new Instruction("OR IXh", 19, () => or(IX.H), 0xDD, 0xB4));
            instructionSet.Add(new Instruction("OR IYh", 19, () => or(IY.H), 0xFD, 0xB4));
            instructionSet.Add(new Instruction("OR IXl", 19, () => or(IX.L), 0xDD, 0xB5));
            instructionSet.Add(new Instruction("OR IYl", 19, () => or(IY.L), 0xFD, 0xB5));

            instructionSet.Add(new Instruction("XOR IXh", 19, () => xor(IX.H), 0xDD, 0xAC));
            instructionSet.Add(new Instruction("XOR IYh", 19, () => xor(IY.H), 0xFD, 0xAC));
            instructionSet.Add(new Instruction("XOR IXl", 19, () => xor(IX.L), 0xDD, 0xAD));
            instructionSet.Add(new Instruction("XOR IYl", 19, () => xor(IY.L), 0xFD, 0xAD));

            instructionSet.Add(new Instruction("CP IXh", 19, () => cp(IX.H), 0xDD, 0xBC));
            instructionSet.Add(new Instruction("CP IYh", 19, () => cp(IY.H), 0xFD, 0xBC));
            instructionSet.Add(new Instruction("CP IXl", 19, () => cp(IX.L), 0xDD, 0xBD));
            instructionSet.Add(new Instruction("CP IYl", 19, () => cp(IY.L), 0xFD, 0xBD));

            instructionSet.Add(new Instruction("INC IXh", 8, () => inc(IX.H), 0xDD, 0x24));
            instructionSet.Add(new Instruction("INC IYh", 8, () => inc(IY.H), 0xFD, 0x24));
            instructionSet.Add(new Instruction("INC IXl", 8, () => inc(IX.L), 0xDD, 0x2C));
            instructionSet.Add(new Instruction("INC IYl", 8, () => inc(IY.L), 0xFD, 0x2C));

            instructionSet.Add(new Instruction("DEC IXh", 8, () => dec(IX.H), 0xDD, 0x25));
            instructionSet.Add(new Instruction("DEC IYh", 8, () => dec(IY.H), 0xFD, 0x25));
            instructionSet.Add(new Instruction("DEC IXl", 8, () => dec(IX.L), 0xDD, 0x2D));
            instructionSet.Add(new Instruction("DEC IYl", 8, () => dec(IY.L), 0xFD, 0x2D));

            instructionSet.Add(new Instruction("JP NN", 10, jp, 0xC3));

            instructionSet.Add(new Instruction("JP NZ, NN", 10, () => jp(NZ), 0xC2 + 0x00));
            instructionSet.Add(new Instruction("JP Z, NN", 10, () => jp(ZF), 0xC2 + 0x08));
            instructionSet.Add(new Instruction("JP NC, NN", 10, () => jp(NC), 0xC2 + 0x10));
            instructionSet.Add(new Instruction("JP C, NN", 10, () => jp(CF), 0xC2 + 0x18));
            instructionSet.Add(new Instruction("JP PO, NN", 10, () => jp(PO), 0xC2 + 0x20));
            instructionSet.Add(new Instruction("JP PE, NN", 10, () => jp(PE), 0xC2 + 0x28));
            instructionSet.Add(new Instruction("JP P, NN", 10, () => jp(!SF), 0xC2 + 0x30));
            instructionSet.Add(new Instruction("JP M, NN", 10, () => jp(SF), 0xC2 + 0x38));

            instructionSet.Add(new Instruction("JP (HL)", 4, () => jp(HL), 0xE9));
            instructionSet.Add(new Instruction("JP (IX)", 8, () => jp(IX), 0xDD, 0xE9));
            instructionSet.Add(new Instruction("JP (IY)", 8, () => jp(IY), 0xFD, 0xE9));
            instructionSet.Add(new Instruction("JR e", 12, jr, 0x18));
            instructionSet.Add(new Instruction("JR NZ, e", 7, () => jr(NZ), 0x20).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("JR Z, e", 7, () => jr(ZF), 0x28).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("JR NC, e", 7, () => jr(NC), 0x30).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("JR C, e", 7, () => jr(CF), 0x38).WithTStatesAlt(5));

            instructionSet.Add(new Instruction("DJNZ e", 8, djnz, 0x10).WithTStatesAlt(5));

            instructionSet.Add(new Instruction("CALL NN", 17, () => call(), 0xCD));
            instructionSet.Add(new Instruction("CALL NZ, NN", 10, () => call(NZ), 0xC4 + 0x00).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL Z, NN", 10, () => call(ZF), 0xC4 + 0x08).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL NC, NN", 10, () => call(NC), 0xC4 + 0x10).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL C, NN", 10, () => call(CF), 0xC4 + 0x18).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL PO, NN", 10, () => call(PO), 0xC4 + 0x20).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL PE, NN", 10, () => call(PE), 0xC4 + 0x28).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL P, NN", 10, () => call(!SF), 0xC4 + 0x30).WithTStatesAlt(7));
            instructionSet.Add(new Instruction("CALL M, NN", 10, () => call(SF), 0xC4 + 0x38).WithTStatesAlt(7));

            instructionSet.Add(new Instruction("RST 00", 11, () => rst(0x00), 0xC7));
            instructionSet.Add(new Instruction("RST 08", 11, () => rst(0x08), 0xCF));
            instructionSet.Add(new Instruction("RST 10", 11, () => rst(0x10), 0xD7));
            instructionSet.Add(new Instruction("RST 18", 11, () => rst(0x18), 0xDF));
            instructionSet.Add(new Instruction("RST 20", 11, () => rst(0x20), 0xE7));
            instructionSet.Add(new Instruction("RST 28", 11, () => rst(0x28), 0xEF));
            instructionSet.Add(new Instruction("RST 30", 11, () => rst(0x30), 0xF7));
            instructionSet.Add(new Instruction("RST 38", 11, () => rst(0x38), 0xFF));

            instructionSet.Add(new Instruction("RET", 10, () => ret(), 0xC9));
            instructionSet.Add(new Instruction("RET NZ", 5, () => ret(NZ), 0xC0).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET Z", 5, () => ret(ZF), 0xC8).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET NC", 5, () => ret(NC), 0xD0).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET C", 5, () => ret(CF), 0xD8).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET PO", 5, () => ret(PO), 0xE0).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET PE", 5, () => ret(PE), 0xE8).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET P", 5, () => ret(!SF), 0xF0).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RET M", 5, () => ret(SF), 0xF8).WithTStatesAlt(6));
            instructionSet.Add(new Instruction("RETI", 14, retin, 0xED, 0x4D));
            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x45));

            instructionSet.Add(new Instruction("INI", 16, ini, 0xED, 0xA2));
            instructionSet.Add(new Instruction("INIR", 16, inir, 0xED, 0xB2).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("IND", 16, ind, 0xED, 0xAA));
            instructionSet.Add(new Instruction("INDR", 16, indr, 0xED, 0xBA).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("OUT (N), A", 11, OutPortN, 0xD3));
            instructionSet.Add(new Instruction("OUTI", 16, outi, 0xED, 0xA3));
            instructionSet.Add(new Instruction("OTIR", 16, otir, 0xED, 0xB3).WithTStatesAlt(5));
            instructionSet.Add(new Instruction("OUTD", 16, outd, 0xED, 0xAB));
            instructionSet.Add(new Instruction("OTDR", 16, otdr, 0xED, 0xBB).WithTStatesAlt(5));

            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x4C));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x54));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x5C));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x64));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x6C));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x74));
            instructionSet.Add(new Instruction("NEG", 8, neg, 0xED, 0x7C));

            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x55));
            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x5D));
            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x65));
            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x6D));
            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x75));
            instructionSet.Add(new Instruction("RETN", 14, retin, 0xED, 0x7D));

            instructionSet.Add(new Instruction("IM 0", 8, () => im(0), 0xED, 0x4E));
            instructionSet.Add(new Instruction("IM 0", 8, () => im(0), 0xED, 0x66));
            instructionSet.Add(new Instruction("IM 0", 8, () => im(0), 0xED, 0x6E));
            instructionSet.Add(new Instruction("IM 1", 8, () => im(1), 0xED, 0x76));
            instructionSet.Add(new Instruction("IM 2", 8, () => im(2), 0xED, 0x7E));
        }
    }
}
