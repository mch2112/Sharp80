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

            instructionSet.Add(new Instruction("ADC N", 0xCE, 7, adc_n));
            instructionSet.Add(new Instruction("ADD N", 0xC6, 7, add_n));
            instructionSet.Add(new Instruction("SUB N", 0xD6, 7, sub_n));
            instructionSet.Add(new Instruction("SBC N", 0xDE, 7, sbc_n));
            instructionSet.Add(new Instruction("AND N", 0xE6, 7, and_n));
            instructionSet.Add(new Instruction("OR N", 0xF6, 7, or_n));
            instructionSet.Add(new Instruction("XOR N", 0xEE, 7, xor_n));
            instructionSet.Add(new Instruction("CP N", 0xFE, 7, cp_n));

            instructionSet.Add(new Instruction("LDI", 0xED, 0xA0, 16, ldi));
            instructionSet.Add(new Instruction("LDIR", 0xED, 0xB0, 16, ldir, 5));
            instructionSet.Add(new Instruction("LDD", 0xED, 0xA8, 16, ldd));
            instructionSet.Add(new Instruction("LDDR", 0xED, 0xB8, 16, lddr, 5));

            instructionSet.Add(new Instruction("CPI", 0xED, 0xA1, 16, cpi));
            instructionSet.Add(new Instruction("CPIR", 0xED, 0xB1, 16, cpir, 5));
            instructionSet.Add(new Instruction("CPD", 0xED, 0xA9, 16, cpd));
            instructionSet.Add(new Instruction("CPDR", 0xED, 0xB9, 16, cpdr, 5));

            instructionSet.Add(new Instruction("DAA", 0x27, 4, daa));
            instructionSet.Add(new Instruction("CPL", 0x2F, 4, cpl));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x44, 8, neg));
            instructionSet.Add(new Instruction("CCF", 0x3F, 4, ccf));
            instructionSet.Add(new Instruction("SCF", 0x37, 4, scf));
            instructionSet.Add(new Instruction("NOP", 0x00, 4, () => { }));
            instructionSet.Add(new Instruction("HALT", 0x76, 6, halt));

            instructionSet.Add(new Instruction("DI", 0xF3, 4, di));
            instructionSet.Add(new Instruction("EI", 0xFB, 4, ei));

            instructionSet.Add(new Instruction("IM 0", 0xED, 0x46, 8, () => im(0)));
            instructionSet.Add(new Instruction("IM 1", 0xED, 0x56, 8, () => im(1)));
            instructionSet.Add(new Instruction("IM 2", 0xED, 0x5E, 8, () => im(2)));

            instructionSet.Add(new Instruction("RLCA", 0x07, 4, rlca));
            instructionSet.Add(new Instruction("RLA", 0x17, 4, rla));
            instructionSet.Add(new Instruction("RRCA", 0x0F, 4, rrca));
            instructionSet.Add(new Instruction("RRA", 0x1F, 4, rra));

            instructionSet.Add(new Instruction("RLD", 0xED, 0x6F, 18, rld));
            instructionSet.Add(new Instruction("RRD", 0xED, 0x67, 18, rrd));

            instructionSet.Add(new Instruction("IN A, (N)", 0xDB, 11, InPortN));
            instructionSet.Add(new Instruction("IN (C)", 0xED, 0x70, 12, InPortZero));
            instructionSet.Add(new Instruction("OUT (C), 0", 0xED, 0x71, 12, OutPortZero));

            foreach (var i in iter8)
            {
                tStates = (byte)(i == HLMIndex ? 7 : 4); // (HL) takes longer

                instructionSet.Add(new Instruction($"ADD {r8[i].Name}", (byte)(0x80 + i), tStates, () => add(r8[i])));
                instructionSet.Add(new Instruction($"ADC {r8[i].Name}", (byte)(0x88 + i), tStates, () => adc(r8[i])));
                instructionSet.Add(new Instruction($"SUB {r8[i].Name}", (byte)(0x90 + i), tStates, () => sub(r8[i])));
                instructionSet.Add(new Instruction($"SBC {r8[i].Name}", (byte)(0x98 + i), tStates, () => sbc(r8[i])));
                instructionSet.Add(new Instruction($"AND {r8[i].Name}", (byte)(0xA0 + i), tStates, () => and(r8[i])));
                instructionSet.Add(new Instruction($"XOR {r8[i].Name}", (byte)(0xA8 + i), tStates, () => xor(r8[i])));
                instructionSet.Add(new Instruction($"OR {r8[i].Name}", (byte)(0xB0 + i), tStates, () => or(r8[i])));
                instructionSet.Add(new Instruction($"CP {r8[i].Name}", (byte)(0xB8 + i), tStates, () => cp(r8[i])));

                tStates = (byte)(i == HLMIndex ? 11 : 4);

                instructionSet.Add(new Instruction($"INC {r8[i].Name}", (byte)(0x04 + i * 0x08), tStates, () => inc(r8[i])));
                instructionSet.Add(new Instruction($"DEC {r8[i].Name}", (byte)(0x05 + i * 0x08), tStates, () => dec(r8[i])));

                tStates = (byte)(i == HLMIndex ? 15 : 8);

                instructionSet.Add(new Instruction($"RLC {r8[i].Name}", 0xCB, i, tStates, () => rlc(r8[i])));
                instructionSet.Add(new Instruction($"RRC {r8[i].Name}", 0xCB, (byte)(i + 0x08), tStates, () => rrc(r8[i])));
                instructionSet.Add(new Instruction($"RL {r8[i].Name}", 0xCB, (byte)(i + 0x10), tStates, () => rl(r8[i])));
                instructionSet.Add(new Instruction($"RR {r8[i].Name}", 0xCB, (byte)(i + 0x18), tStates, () => rr(r8[i])));
                instructionSet.Add(new Instruction($"SLA {r8[i].Name}", 0xCB, (byte)(i + 0x20), tStates, () => sla(r8[i])));
                instructionSet.Add(new Instruction($"SRA {r8[i].Name}", 0xCB, (byte)(i + 0x28), tStates, () => sra(r8[i])));
                instructionSet.Add(new Instruction($"SLL {r8[i].Name}", 0xCB, (byte)(i + 0x30), tStates, () => sll(r8[i])));
                instructionSet.Add(new Instruction($"SRL {r8[i].Name}", 0xCB, (byte)(i + 0x38), tStates, () => srl(r8[i])));

                tStates = (byte)(i == HLMIndex ? 10 : 7);

                instructionSet.Add(new Instruction($"LD {r8[i].Name}, N", (byte)(i * 0x08 + 0x06), tStates, () => load_reg_nn(r8[i])));

                if (i != HLMIndex)
                {
                    foreach (var j in iter2)
                    {
                        instructionSet.Add(new Instruction($"LD {r8[i].Name}, ({rxyi[j].Proxy.Name}+d)", (byte)(0xDD + 0x20 * j), (byte)(0x46 + i * 0x08), 19, () => load(r8[i], rxyi[j])));
                        instructionSet.Add(new Instruction($"LD ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), (byte)(0x70 + i), 19, () => load(rxyi[j], r8[i])));

                        // COMPOUND INSTRUCTIONS (UNDOCUMENTED)
                        instructionSet.Add(new Instruction($"RLC ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, i, 23, () => rlc_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"RRC ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x08 + i), 23, () => rrc_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"RL ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x10 + i), 23, () => rl_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"RR ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x18 + i), 23, () => rr_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"SLA ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x20 + i), 23, () => sla_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"SRA ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x28 + i), 23, () => sra_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"SLL ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x30 + i), 23, () => sll_compound(rxyi[j], r8[i])));
                        instructionSet.Add(new Instruction($"SRL ({rxyi[j].Proxy.Name}+d), {r8[i].Name}", (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x38 + i), 23, () => srl_compound(rxyi[j], r8[i])));
                        foreach (var k in iter8)
                        {
                            instructionSet.Add(new Instruction(string.Format("RES {0}, ({1}+d), {2}", k, rxyi[j].Proxy.Name, r8[i].Name), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0x80 + k * 0x08 + i), 23, () => res_compound(rxyi[j], r8[i], k)));
                            instructionSet.Add(new Instruction(string.Format("SET {0}, ({1}+d), {2}", k, rxyi[j].Proxy.Name, r8[i].Name), (byte)(0xDD + 0x20 * j), 0xCB, (byte)(0xC0 + k * 0x08 + i), 23, () => set_compound(rxyi[j], r8[i], k)));
                        }
                    }
                    instructionSet.Add(new Instruction($"LD {r8x[i].Name}, IXh", 0xDD, (byte)(0x44 + i * 0x08), 8, () => load(r8x[i], IX.H)));
                    instructionSet.Add(new Instruction($"LD {r8x[i].Name}, IXl", 0xDD, (byte)(0x45 + i * 0x08), 8, () => load(r8x[i], IX.L)));

                    instructionSet.Add(new Instruction($"LD {r8y[i].Name}, IYh", 0xFD, (byte)(0x44 + i * 0x08), 8, () => load(r8y[i], IY.H)));
                    instructionSet.Add(new Instruction($"LD {r8y[i].Name}, IYl", 0xFD, (byte)(0x45 + i * 0x08), 8, () => load(r8y[i], IY.L)));

                    if (i <= 3 || i == 7) // r is B, C, D, E, or A
                    {
                        instructionSet.Add(new Instruction($"LD IXh, {r8x[i].Name}", 0xDD, (byte)(0x60 + i), 8, () => load(IX.H, r8x[i])));
                        instructionSet.Add(new Instruction($"LD IXl, {r8x[i].Name}", 0xDD, (byte)(0x68 + i), 8, () => load(IX.L, r8x[i])));

                        instructionSet.Add(new Instruction($"LD IYh, {r8y[i].Name}", 0xFD, (byte)(0x60 + i), 8, () => load(IY.H, r8y[i])));
                        instructionSet.Add(new Instruction($"LD IYl, {r8y[i].Name}", 0xFD, (byte)(0x68 + i), 8, () => load(IY.L, r8y[i])));
                    }

                    instructionSet.Add(new Instruction($"IN {r8[i].Name}, (C)", 0xED, (byte)(0x40 + i * 0x08), 12, () => InPortR(r8[i])));
                    instructionSet.Add(new Instruction($"OUT (C), {r8[i].Name}", 0xED, (byte)(0x41 + i * 0x08), 12, () => OutPortR(r8[i])));
                }

                foreach (var j in iter2)
                {
                    instructionSet.Add(new Instruction($"SET {i}, ({rxyi[j].Proxy.Name}+d)", (byte)(0xDD + j * 0x20), 0xCB, (byte)(0xC6 + i * 0x08), 23, () => set(rxyi[j], i)));
                    instructionSet.Add(new Instruction($"RES {i}, ({rxyi[j].Proxy.Name}+d)", (byte)(0xDD + j * 0x20), 0xCB, (byte)(0x86 + i * 0x08), 23, () => res(rxyi[j], i)));
                }

                foreach (var j in iter8)
                {
                    tStates = (byte)(i == HLMIndex || j == HLMIndex ? 7 : 4); // (HL) takes longer
                    opCode = (byte)(0x40 + 0x08 * i + j);
                    if (opCode != 0x76) // halt
                        instructionSet.Add(new Instruction($"LD {r8[i].Name}, {r8[j].Name}", opCode, tStates, () => load(r8[i], r8[j])));

                    tStates = (byte)(i == HLMIndex ? 12 : 8);

                    instructionSet.Add(new Instruction($"BIT {j}, {r8[i].Name}", 0xCB, (byte)(0x40 + j * 0x08 + i), tStates,
                        i == HLMIndex ? (Instruction.InstructionDelegate)(() => bitHLM(j))
                                      : (Instruction.InstructionDelegate)(() => bit(r8[i], j))));

                    instructionSet.Add(new Instruction($"SET {j}, {r8[i].Name}", 0xCB, (byte)(0xC0 + j * 0x08 + i), tStates, () => set(r8[i], j)));
                    instructionSet.Add(new Instruction($"RES {j}, {r8[i].Name}", 0xCB, (byte)(0x80 + j * 0x08 + i), tStates, () => res(r8[i], j)));

                    foreach (var k in iter2)
                    {
                        // redundant opcodes
                        instructionSet.Add(new Instruction($"BIT {i}, ({rxyi[k].Proxy.Name}+d)", (byte)(0xDD + k * 0x20), 0xCB, (byte)(0x40 + i * 8 + j), 20, () => bit(rxyi[k], i)));
                    }
                }
            }

            foreach (var i in iter4)
            {
                instructionSet.Add(new Instruction(string.Format("LD {0}, NN", r16s[i].Name), (byte)(0x01 + i * 0x10), 10, () => load_reg_nnnn(r16s[i])));
                instructionSet.Add(new Instruction(string.Format("LD {0}, (NN)", r16s[i].Name), 0xED, (byte)(0x4B + i * 0x10), 20, () => load_xx_mmmm(r16s[i])));
                instructionSet.Add(new Instruction(string.Format("LD (NN), {0}", r16s[i].Name), 0xED, (byte)(0x43 + i * 0x10), 20, () => load_mmmm_xx(r16s[i])));
                instructionSet.Add(new Instruction(string.Format("INC {0}", r16s[i].Name), (byte)(0x03 + i * 0x10), 6, () => inc(r16s[i])));
                instructionSet.Add(new Instruction(string.Format("DEC {0}", r16s[i].Name), (byte)(0x0B + i * 0x10), 6, () => dec(r16s[i])));
                instructionSet.Add(new Instruction(string.Format("ADD HL, {0}", r16s[i].Name), (byte)(0x09 + i * 0x10), 11, () => add(HL, r16s[i])));
                instructionSet.Add(new Instruction(string.Format("SBC HL, {0}", r16s[i].Name), 0xED, (byte)(0x42 + i * 0x10), 15, () => sbc(HL, r16s[i])));
                instructionSet.Add(new Instruction(string.Format("ADC HL, {0}", r16s[i].Name), 0xED, (byte)(0x4A + i * 0x10), 15, () => adc(HL, r16s[i])));

                instructionSet.Add(new Instruction(string.Format("POP {0}", r16a[i].Name), (byte)(0xC1 + i * 0x10), 10, () => pop(r16a[i])));
                instructionSet.Add(new Instruction(string.Format("PUSH {0}", r16a[i].Name), (byte)(0xC5 + i * 0x10), 11, () => push(r16a[i])));

                instructionSet.Add(new Instruction(string.Format("ADD {0}, {1}", IX.Name, r16xs[i].Name), 0xDD, (byte)(0x09 + i * 0x10), 15, () => add(IX, r16xs[i])));
                instructionSet.Add(new Instruction(string.Format("ADD {0}, {1}", IY.Name, r16ys[i].Name), 0xFD, (byte)(0x09 + i * 0x10), 15, () => add(IY, r16ys[i])));
            }
            foreach (var i in iter2)
            {
                instructionSet.Add(new Instruction(string.Format("LD ({0}+d), N", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x36, 10, () => load_ixy_nn(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("LD {0}, NN", rxy[i].Name), (byte)(0xDD + i * 0x20), 0x21, 14, () => load_reg_nnnn(rxy[i])));
                instructionSet.Add(new Instruction(string.Format("LD (NN), {0}", rxy[i].Name), (byte)(0xDD + i * 0x20), 0x22, 20, () => load_mmmm_ixy(rxy[i])));
                instructionSet.Add(new Instruction(string.Format("LD {0}, (NN)", rxy[i].Name), (byte)(0xDD + i * 0x20), 0x2A, 20, () => load_ixy_mmmm(rxy[i])));
                instructionSet.Add(new Instruction(string.Format("INC {0}", rxy[i].Name), (byte)(0xDD + i * 0x20), 0x23, 10, () => inc(rxy[i])));
                instructionSet.Add(new Instruction(string.Format("DEC {0}", rxy[i].Name), (byte)(0xDD + i * 0x20), 0x2B, 10, () => dec(rxy[i])));
                instructionSet.Add(new Instruction(string.Format("POP {0}", rxy[i].Name), (byte)(0xDD + i * 0x20), 0xE1, 14, () => pop(rxy[i])));
                instructionSet.Add(new Instruction(string.Format("PUSH {0}", rxy[i].Name), (byte)(0xDD + i * 0x20), 0xE5, 15, () => push(rxy[i])));

                instructionSet.Add(new Instruction(string.Format("INC ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x34, 23, () => inc(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("DEC ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x35, 23, () => dec(rxyi[i])));

                instructionSet.Add(new Instruction(string.Format("ADD ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x86, 19, () => add(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("ADC ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x8E, 19, () => adc(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("SUB ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x96, 19, () => sub(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("SBC ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0x9E, 19, () => sbc(rxyi[i])));

                instructionSet.Add(new Instruction(string.Format("AND ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xA6, 19, () => and(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("XOR ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xAE, 19, () => xor(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("OR ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xB6, 19, () => or(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("CP ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xBE, 19, () => cp(rxyi[i])));

                instructionSet.Add(new Instruction(string.Format("RLC ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x06, 23, () => rlc(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("RRC ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x0E, 23, () => rrc(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("RL ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x16, 23, () => rl(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("RR ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x1E, 23, () => rr(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("SLA ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x26, 23, () => sla(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("SRA ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x2E, 23, () => sra(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("SLL ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x36, 23, () => sll(rxyi[i])));
                instructionSet.Add(new Instruction(string.Format("SRL ({0}+d)", rxyi[i].Proxy.Name), (byte)(0xDD + i * 0x20), 0xCB, 0x3E, 23, () => srl(rxyi[i])));
            }

            instructionSet.Add(new Instruction("LD A, (BC)", 0x0A, 7, () => load(A, BCM)));
            instructionSet.Add(new Instruction("LD A, (DE)", 0x1A, 7, () => load(A, DEM)));
            instructionSet.Add(new Instruction("LD A, (NN)", 0x3A, 13, load_a_mmmm));

            instructionSet.Add(new Instruction("LD (BC), A", 0x02, 7, () => load(BCM, A)));
            instructionSet.Add(new Instruction("LD (DE), A", 0x12, 7, () => load(DEM, A)));
            instructionSet.Add(new Instruction("LD (NN), A", 0x32, 13, load_mmmm_a));

            instructionSet.Add(new Instruction("LD A, I", 0xED, 0x57, 9, load_a_i));
            instructionSet.Add(new Instruction("LD A, R", 0xED, 0x5F, 9, load_a_r));
            instructionSet.Add(new Instruction("LD I, A", 0xED, 0x47, 9, () => load(I, A)));
            instructionSet.Add(new Instruction("LD R, A", 0xED, 0x4F, 9, () => load(R, A)));

            instructionSet.Add(new Instruction("LD HL, (NN)", 0x2A, 16, () => load_xx_mmmm(HL)));
            instructionSet.Add(new Instruction("LD (NN), HL", 0x22, 16, () => load_mmmm_xx(HL)));

            instructionSet.Add(new Instruction("LD SP, HL", 0xF9, 6, () => load(SP, HL)));
            instructionSet.Add(new Instruction("LD SP, IX", 0xDD, 0xF9, 10, () => load(SP, IX)));
            instructionSet.Add(new Instruction("LD SP, IY", 0xFD, 0xF9, 10, () => load(SP, IY)));

            instructionSet.Add(new Instruction("LD IXh, N", 0xDD, 0x26, 11, () => load_reg_nn(IX.H)));
            instructionSet.Add(new Instruction("LD IYh, N", 0xFD, 0x26, 11, () => load_reg_nn(IY.H)));
            instructionSet.Add(new Instruction("LD IXl, N", 0xDD, 0x2E, 11, () => load_reg_nn(IX.L)));
            instructionSet.Add(new Instruction("LD IYl, N", 0xFD, 0x2E, 11, () => load_reg_nn(IY.L)));

            instructionSet.Add(new Instruction("EXX", 0xD9, 4, exx));
            instructionSet.Add(new Instruction("EX DE, HL", 0xEB, 4, () => ex(DE, HL)));
            instructionSet.Add(new Instruction("EX AF, AF'", 0x08, 4, () => ex(AF, AFp)));
            instructionSet.Add(new Instruction("EX (SP), HL", 0xE3, 19, () => ex_spm(HL)));
            instructionSet.Add(new Instruction("EX (SP), IX", 0xDD, 0xE3, 23, () => ex_spm(IX)));
            instructionSet.Add(new Instruction("EX (SP), IY", 0xFD, 0xE3, 23, () => ex_spm(IY)));

            instructionSet.Add(new Instruction("ADD IXh", 0xDD, 0x84, 19, () => add(IX.H)));
            instructionSet.Add(new Instruction("ADD IYh", 0xFD, 0x84, 19, () => add(IY.H)));
            instructionSet.Add(new Instruction("ADD IXl", 0xDD, 0x85, 19, () => add(IX.L)));
            instructionSet.Add(new Instruction("ADD IYl", 0xFD, 0x85, 19, () => add(IY.L)));

            instructionSet.Add(new Instruction("ADC IXh", 0xDD, 0x8C, 19, () => adc(IX.H)));
            instructionSet.Add(new Instruction("ADC IYh", 0xFD, 0x8C, 19, () => adc(IY.H)));
            instructionSet.Add(new Instruction("ADC IXl", 0xDD, 0x8D, 19, () => adc(IX.L)));
            instructionSet.Add(new Instruction("ADC IYl", 0xFD, 0x8D, 19, () => adc(IY.L)));

            instructionSet.Add(new Instruction("SUB IXh", 0xDD, 0x94, 19, () => sub(IX.H)));
            instructionSet.Add(new Instruction("SUB IYh", 0xFD, 0x94, 19, () => sub(IY.H)));
            instructionSet.Add(new Instruction("SUB IXl", 0xDD, 0x95, 19, () => sub(IX.L)));
            instructionSet.Add(new Instruction("SUB IYl", 0xFD, 0x95, 19, () => sub(IY.L)));

            instructionSet.Add(new Instruction("SBC IXh", 0xDD, 0x9C, 19, () => sbc(IX.H)));
            instructionSet.Add(new Instruction("SBC IYh", 0xFD, 0x9C, 19, () => sbc(IY.H)));
            instructionSet.Add(new Instruction("SBC IXl", 0xDD, 0x9D, 19, () => sbc(IX.L)));
            instructionSet.Add(new Instruction("SBC IYl", 0xFD, 0x9D, 19, () => sbc(IY.L)));

            instructionSet.Add(new Instruction("AND IXh", 0xDD, 0xA4, 19, () => and(IX.H)));
            instructionSet.Add(new Instruction("AND IYh", 0xFD, 0xA4, 19, () => and(IY.H)));
            instructionSet.Add(new Instruction("AND IXl", 0xDD, 0xA5, 19, () => and(IX.L)));
            instructionSet.Add(new Instruction("AND IYl", 0xFD, 0xA5, 19, () => and(IY.L)));

            instructionSet.Add(new Instruction("OR IXh", 0xDD, 0xB4, 19, () => or(IX.H)));
            instructionSet.Add(new Instruction("OR IYh", 0xFD, 0xB4, 19, () => or(IY.H)));
            instructionSet.Add(new Instruction("OR IXl", 0xDD, 0xB5, 19, () => or(IX.L)));
            instructionSet.Add(new Instruction("OR IYl", 0xFD, 0xB5, 19, () => or(IY.L)));

            instructionSet.Add(new Instruction("XOR IXh", 0xDD, 0xAC, 19, () => xor(IX.H)));
            instructionSet.Add(new Instruction("XOR IYh", 0xFD, 0xAC, 19, () => xor(IY.H)));
            instructionSet.Add(new Instruction("XOR IXl", 0xDD, 0xAD, 19, () => xor(IX.L)));
            instructionSet.Add(new Instruction("XOR IYl", 0xFD, 0xAD, 19, () => xor(IY.L)));

            instructionSet.Add(new Instruction("CP IXh", 0xDD, 0xBC, 19, () => cp(IX.H)));
            instructionSet.Add(new Instruction("CP IYh", 0xFD, 0xBC, 19, () => cp(IY.H)));
            instructionSet.Add(new Instruction("CP IXl", 0xDD, 0xBD, 19, () => cp(IX.L)));
            instructionSet.Add(new Instruction("CP IYl", 0xFD, 0xBD, 19, () => cp(IY.L)));

            instructionSet.Add(new Instruction("INC IXh", 0xDD, 0x24, 8, () => inc(IX.H)));
            instructionSet.Add(new Instruction("INC IYh", 0xFD, 0x24, 8, () => inc(IY.H)));
            instructionSet.Add(new Instruction("INC IXl", 0xDD, 0x2C, 8, () => inc(IX.L)));
            instructionSet.Add(new Instruction("INC IYl", 0xFD, 0x2C, 8, () => inc(IY.L)));

            instructionSet.Add(new Instruction("DEC IXh", 0xDD, 0x25, 8, () => dec(IX.H)));
            instructionSet.Add(new Instruction("DEC IYh", 0xFD, 0x25, 8, () => dec(IY.H)));
            instructionSet.Add(new Instruction("DEC IXl", 0xDD, 0x2D, 8, () => dec(IX.L)));
            instructionSet.Add(new Instruction("DEC IYl", 0xFD, 0x2D, 8, () => dec(IY.L)));

            instructionSet.Add(new Instruction("JP NN", 0xC3, 10, jp));

            instructionSet.Add(new Instruction("JP NZ, NN", 0xC2 + 0x00, 10, () => jp(NZ)));
            instructionSet.Add(new Instruction("JP Z, NN", 0xC2 + 0x08, 10, () => jp(ZF)));
            instructionSet.Add(new Instruction("JP NC, NN", 0xC2 + 0x10, 10, () => jp(NC)));
            instructionSet.Add(new Instruction("JP C, NN", 0xC2 + 0x18, 10, () => jp(CF)));
            instructionSet.Add(new Instruction("JP PO, NN", 0xC2 + 0x20, 10, () => jp(PO)));
            instructionSet.Add(new Instruction("JP PE, NN", 0xC2 + 0x28, 10, () => jp(PE)));
            instructionSet.Add(new Instruction("JP P, NN", 0xC2 + 0x30, 10, () => jp(!SF)));
            instructionSet.Add(new Instruction("JP M, NN", 0xC2 + 0x38, 10, () => jp(SF)));

            instructionSet.Add(new Instruction("JP (HL)", 0xE9, 4, () => jp(HL)));
            instructionSet.Add(new Instruction("JP (IX)", 0xDD, 0xE9, 8, () => jp(IX)));
            instructionSet.Add(new Instruction("JP (IY)", 0xFD, 0xE9, 8, () => jp(IY)));
            instructionSet.Add(new Instruction("JR e", 0x18, 12, jr));
            instructionSet.Add(new Instruction("JR NZ, e", 0x20, 7, () => jr(NZ), 5));
            instructionSet.Add(new Instruction("JR Z, e", 0x28, 7, () => jr(ZF), 5));
            instructionSet.Add(new Instruction("JR NC, e", 0x30, 7, () => jr(NC), 5));
            instructionSet.Add(new Instruction("JR C, e", 0x38, 7, () => jr(CF), 5));

            instructionSet.Add(new Instruction("DJNZ e", 0x10, 8, djnz, 5));

            instructionSet.Add(new Instruction("CALL NN", 0xCD, 17, () => call()));
            instructionSet.Add(new Instruction("CALL NZ, NN", 0xC4 + 0x00, 10, () => call(NZ), 7));
            instructionSet.Add(new Instruction("CALL Z, NN", 0xC4 + 0x08, 10, () => call(ZF), 7));
            instructionSet.Add(new Instruction("CALL NC, NN", 0xC4 + 0x10, 10, () => call(NC), 7));
            instructionSet.Add(new Instruction("CALL C, NN", 0xC4 + 0x18, 10, () => call(CF), 7));
            instructionSet.Add(new Instruction("CALL PO, NN", 0xC4 + 0x20, 10, () => call(PO), 7));
            instructionSet.Add(new Instruction("CALL PE, NN", 0xC4 + 0x28, 10, () => call(PE), 7));
            instructionSet.Add(new Instruction("CALL P, NN", 0xC4 + 0x30, 10, () => call(!SF), 7));
            instructionSet.Add(new Instruction("CALL M, NN", 0xC4 + 0x38, 10, () => call(SF), 7));

            instructionSet.Add(new Instruction("RST 00", 0xC7, 11, () => rst(0x00)));
            instructionSet.Add(new Instruction("RST 08", 0xCF, 11, () => rst(0x08)));
            instructionSet.Add(new Instruction("RST 10", 0xD7, 11, () => rst(0x10)));
            instructionSet.Add(new Instruction("RST 18", 0xDF, 11, () => rst(0x18)));
            instructionSet.Add(new Instruction("RST 20", 0xE7, 11, () => rst(0x20)));
            instructionSet.Add(new Instruction("RST 28", 0xEF, 11, () => rst(0x28)));
            instructionSet.Add(new Instruction("RST 30", 0xF7, 11, () => rst(0x30)));
            instructionSet.Add(new Instruction("RST 38", 0xFF, 11, () => rst(0x38)));

            instructionSet.Add(new Instruction("RET", 0xC9, 10, () => ret()));
            instructionSet.Add(new Instruction("RET NZ", 0xC0, 5, () => ret(NZ), 6));
            instructionSet.Add(new Instruction("RET Z", 0xC8, 5, () => ret(ZF), 6));
            instructionSet.Add(new Instruction("RET NC", 0xD0, 5, () => ret(NC), 6));
            instructionSet.Add(new Instruction("RET C", 0xD8, 5, () => ret(CF), 6));
            instructionSet.Add(new Instruction("RET PO", 0xE0, 5, () => ret(PO), 6));
            instructionSet.Add(new Instruction("RET PE", 0xE8, 5, () => ret(PE), 6));
            instructionSet.Add(new Instruction("RET P", 0xF0, 5, () => ret(!SF), 6));
            instructionSet.Add(new Instruction("RET M", 0xF8, 5, () => ret(SF), 6));
            instructionSet.Add(new Instruction("RETI", 0xED, 0x4D, 14, retin));
            instructionSet.Add(new Instruction("RETN", 0xED, 0x45, 14, retin));

            instructionSet.Add(new Instruction("INI", 0xED, 0xA2, 16, ini));
            instructionSet.Add(new Instruction("INIR", 0xED, 0xB2, 16, inir, 5));
            instructionSet.Add(new Instruction("IND", 0xED, 0xAA, 16, ind));
            instructionSet.Add(new Instruction("INDR", 0xED, 0xBA, 16, indr, 5));
            instructionSet.Add(new Instruction("OUT (N), A", 0xD3, 11, OutPortN));
            instructionSet.Add(new Instruction("OUTI", 0xED, 0xA3, 16, outi));
            instructionSet.Add(new Instruction("OTIR", 0xED, 0xB3, 16, otir, 5));
            instructionSet.Add(new Instruction("OUTD", 0xED, 0xAB, 16, outd));
            instructionSet.Add(new Instruction("OTDR", 0xED, 0xBB, 16, otdr, 5));

            instructionSet.Add(new Instruction("NEG", 0xED, 0x4C, 8, neg));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x54, 8, neg));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x5C, 8, neg));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x64, 8, neg));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x6C, 8, neg));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x74, 8, neg));
            instructionSet.Add(new Instruction("NEG", 0xED, 0x7C, 8, neg));

            instructionSet.Add(new Instruction("RETN", 0xED, 0x55, 14, retin));
            instructionSet.Add(new Instruction("RETN", 0xED, 0x5D, 14, retin));
            instructionSet.Add(new Instruction("RETN", 0xED, 0x65, 14, retin));
            instructionSet.Add(new Instruction("RETN", 0xED, 0x6D, 14, retin));
            instructionSet.Add(new Instruction("RETN", 0xED, 0x75, 14, retin));
            instructionSet.Add(new Instruction("RETN", 0xED, 0x7D, 14, retin));

            instructionSet.Add(new Instruction("IM 0", 0xED, 0x4E, 8, () => im(0)));
            instructionSet.Add(new Instruction("IM 0", 0xED, 0x66, 8, () => im(0)));
            instructionSet.Add(new Instruction("IM 0", 0xED, 0x6E, 8, () => im(0)));
            instructionSet.Add(new Instruction("IM 1", 0xED, 0x76, 8, () => im(1)));
            instructionSet.Add(new Instruction("IM 2", 0xED, 0x7E, 8, () => im(2)));
        }
    }
}
