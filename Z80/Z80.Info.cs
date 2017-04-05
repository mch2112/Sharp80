/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Linq;

namespace Sharp80.Z80
{
    public partial class Z80 : IStatus
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

        public string GetInternalsReport() => $"{flagsToString[F.val]}\r\nPC   {PC}\r\nSP   {SP}\r\n\r\nAF   {AF}\r\nBC   {BC}\r\nDE   {DE}\r\nHL   {HL}\r\n\r\nIX   {IX}\r\nIY   {IY}\r\n\r\nAF'  {AFp}\r\nBC'  {BCp}\r\nDE'  {DEp}\r\nHL'  {HLp}\r\n\r\nIR   {I}{R}\r\nWZ   {WZ}\r\n\r\n(HL) {HLM}\r\n(SP) {SPM}";
        
        public string Disassemble(ushort Start, ushort End, bool MakeAssemblable) => disassembler.Disassemble(Memory, Start, End, MakeAssemblable);

        public string GetDisassembly() => HistoricDisassemblyMode ? GetDisassemblyHistoric() : GetDisassemblyNormal();

        public string GetDisassemblyNormal()
        {
            ushort startLocation = PC.val;

            const int MAX_HIGHLIGHT_LINE = NUM_DISASSEMBLY_LINES - 4;

            int idx;
            if ((idx = Array.IndexOf(disassemblyAddresses, startLocation)) > 0)
                startLocation = disassemblyAddresses[(idx > MAX_HIGHLIGHT_LINE) ? MAX_HIGHLIGHT_LINE : 0];

            int j = 0;

            return string.Join(Environment.NewLine,
                               disassemblyAddresses
                                         .Select(i =>
                                         new { addr = disassemblyAddresses[j++] = startLocation })
                                         .Select(n => GetLineInfo((n.addr == PC.val) ? ">" : " ", ref startLocation, GetInstructionAt(n.addr))));
        }
        public string GetDisassemblyHistoric()
        {
            return string.Join(Environment.NewLine,
                               historyBuffer.Select(i => new { addr = i, inst = GetInstructionAt(i) })
                                            .Select(n => string.Format("{0}{1} {2} {3}",
                                                                        (PC.val == n.addr) ? ">" : " ",
                                                                        n.addr.ToHexString(),
                                                                        Lib.GetSpacedHex(Memory, n.addr, n.inst.Size),
                                                                        n.inst.FullName(Memory, n.addr))));
        }
        internal string GetLineInfo(string Prefix, ushort PC, Instruction inst)
        {
            return string.Format("{0}{1}  {2} {3}", Prefix, PC.ToHexString(), Lib.GetSpacedHex(Memory, PC, inst.Size), inst.FullName(Memory, PC));
        }
        internal string GetLineInfo(string Prefix, ref ushort PC, Instruction inst)
        {
            var s = string.Format("{0}{1}  {2} {3}", Prefix, PC.ToHexString(), Lib.GetSpacedHex(Memory, PC, inst.Size), inst.FullName(Memory, PC));
            PC += inst.Size;
            return s;
        }
        public string GetLineInfo(ushort PC) => GetLineInfo(string.Empty, PC, GetInstructionAt(PC));

        public string GetInstructionSetReport() => instructionSet.GetInstructionSetReport();

        public ushort PcVal => PC.val;
        public ushort SpVal => SP.val;
        public ushort AfVal => AF.val;
        public byte AVal => A.val;
        public ushort BcVal => BC.val;
        public ushort DeVal => DE.val;
        public ushort HlVal => HL.val;
        public ushort IxVal => IX.val;
        public ushort IyVal => IY.val;
        public ushort AfpVal => AFp.val;
        public ushort BcpVal => BCp.val;
        public ushort DepVal => DEp.val;
        public ushort HlpVal => HLp.val;
        public byte HlmVal => HLM.val;
        public ushort WzVal => WZ.val;
        public string Flags => flagsToString[F.val];
        public ushort IrVal => (ushort)((I.val << 8) | R.val);
    }
}
