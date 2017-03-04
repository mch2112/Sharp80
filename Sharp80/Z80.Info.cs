/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80.Processor
{
    internal partial class Z80 : IZ80_Status
    {
        private ushort[] disassemblyAddresses = new ushort[NUM_DISASSEMBLY_LINES];
        private static string[] flagsToString = new string[0x100];

        private static void InitFlagsString()
        {
            byte b = 0x00;
            do
            {
                flagsToString[b] = (b.IsBitSet(7) ? "S" : "-") +
                                   (b.IsBitSet(6) ? "Z" : "-") +
                                   (b.IsBitSet(5) ? "5" : "-") +
                                   (b.IsBitSet(4) ? "H" : "-") +
                                   (b.IsBitSet(3) ? "3" : "-") +
                                   (b.IsBitSet(2) ? "V" : "-") +
                                   (b.IsBitSet(1) ? "N" : "-") +
                                   (b.IsBitSet(0) ? "C" : "-");
            }
            while (b++ < 0xFF);
        }

        public string GetInternalsReport()
        {
            return string.Format("{0}\r\nPC   {1}\r\nSP   {2}\r\n\r\nAF   {3}\r\nBC   {4}\r\nDE   {5}\r\nHL   {6}\r\n\r\nIX   {7}\r\nIY   {8}\r\n\r\nAF'  {9}\r\nBC'  {10}\r\nDE'  {11}\r\nHL'  {12}\r\n\r\nIR   {13}{14}\r\nWZ   {15}\r\n\r\n(HL) {16}\r\n(SP) {17}", flagsToString[F.val], PC, SP, AF, BC, DE, HL, IX, IY, AFp, BCp, DEp, HLp, I, R, WZ, HLM, SPM);
        }
        public string GetDisassembly()
        {
            if (HistoricDisassemblyMode)
            {
                var history = new ushort[historyBuffer.Length];
                for (int i = 0; i < historyBuffer.Length; i++)
                    history[(i - historyBufferCursor + historyBuffer.Length - 1) % historyBuffer.Length] = historyBuffer[i];

                if (historyInstructionCount < (ulong) historyBuffer.Length)
                {
                    return GetHistoricDisassembly(history,
                                                  history.Length - (int) historyInstructionCount - 1,
                                                  PC.val);
                }
                else
                {
                    
                    return GetHistoricDisassembly(history, 0, PC.val);
                }
            }
            else
            {
                return GetDisassembly(PC.val, PC.val);
            }
        }

        public string GetDisassembly(ushort StartLocation, ushort HighLight)
        {
            const int MAX_HIGHLIGHT_LINE = NUM_DISASSEMBLY_LINES - 4;

            int idx;
            if ((idx = Array.IndexOf(disassemblyAddresses, StartLocation)) > 0)
                StartLocation = disassemblyAddresses[(idx > MAX_HIGHLIGHT_LINE) ? MAX_HIGHLIGHT_LINE : 0];

            return string.Join(Environment.NewLine,
                               Enumerable.Range(0, NUM_DISASSEMBLY_LINES)
                                         .Select(i => new { addr = disassemblyAddresses[i] = StartLocation })
                                         .Select(n => GetLineInfo((n.addr == HighLight) ? ">" : " ", ref StartLocation, GetInstructionAt(n.addr))));
        }
        public string GetLineInfo(string Prefix, ushort PC, Instruction inst)
        {
            return string.Format("{0}{1}  {2,-11} {3}", Prefix, PC.ToHexString(), Lib.GetSpacedHex(Memory, PC, inst.Size), inst.Name(memory, PC));
        }
        public string GetLineInfo(string Prefix, ref ushort PC, Instruction inst)
        {
            var s = string.Format("{0}{1}  {2,-11} {3}", Prefix, PC.ToHexString(), Lib.GetSpacedHex(Memory, PC, inst.Size), inst.Name(memory, PC));
            PC += inst.Size;
            return s;
        }
        public string GetLineInfo(ushort PC)
        {
            return GetLineInfo(string.Empty, PC, GetInstructionAt(PC));
        }
        public string GetInstructionSetReport()
        {
            return instructionSet.GetInstructionSetReport();
        }
        public string Disassemble(bool AddressAsComment, bool FromPC)
        {
            int PC = FromPC ? this.PC.val : 0;
            Instruction inst;

            // Eliminate trailing NOPs
            ushort lastAddress = 0xFFFF;
            while (memory[lastAddress] == 0 && lastAddress > 0) // NOP
                lastAddress--;
            int end = lastAddress + 1;

            if (end > 0xFFF0)
                end = 0x10000;

            var sb = new StringBuilder(500000);

            var li = new Dictionary<ushort, Instruction>();

            while (PC < end)
            {
                li.Add((ushort)PC, inst = GetInstructionAt((ushort)PC));
                PC += inst.Size;
            }

            return string.Join(Environment.NewLine, li.Select(i => string.Format("{0}  {1,-11} {2}",
                                                                                 i.Key.ToHexString(),
                                                                                 Lib.GetSpacedHex(Memory, i.Key, i.Value.Size),
                                                                                 AddressAsComment ? i.Value.NameWithRelativeAddressesAsComments(memory, i.Key)
                                                                                                  : i.Value.Name(memory, i.Key)
                                                                                 )));
        }

        public string GetHistoricDisassembly(ushort[] History, int historyCursor, ushort HighLight)
        {
            return string.Join(Environment.NewLine,
                               Enumerable.Range(historyCursor, History.Length - historyCursor)
                               .Select(i => new { idx = i, addr = History[i], inst = GetInstructionAt(History[i]) })
                               .Select(n => string.Format("{0}{1} {2,-11} {3}",
                                                           (n.idx == History.Length - 1) ? ">" : " ",
                                                           n.addr.ToHexString(),
                                                           Lib.GetSpacedHex(Memory, n.addr, n.inst.Size),
                                                           n.inst.Name(memory, n.addr))));
        }
        public ushort PcVal { get { return PC.val; } }
        public ushort SpVal { get { return SP.val; } }
        public ushort AfVal {  get { return AF.val; } }
        public ushort BcVal {  get { return BC.val; } }
        public ushort DeVal {  get { return DE.val; } }
        public ushort HlVal {  get { return HL.val; } }
        public ushort IxVal {  get { return IX.val; } }
        public ushort IyVal {  get { return IY.val; } }
        public ushort AfpVal { get { return AFp.val; } }
        public ushort BcpVal { get { return BCp.val; } }
        public ushort DepVal { get { return DEp.val; } }
        public ushort HlpVal { get { return HLp.val; } }
        public ushort WzVal { get { return WZ.val; } }
        public ushort IrVal { get { return (ushort)((I.val << 8) | R.val); } }
    }
}
