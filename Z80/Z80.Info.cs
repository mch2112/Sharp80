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

        public string GetInternalsReport() => $"{flagsToString[F.Value]}\r\nPC   {PC}\r\nSP   {SP}\r\n\r\nAF   {AF}\r\nBC   {BC}\r\nDE   {DE}\r\nHL   {HL}\r\n\r\nIX   {IX}\r\nIY   {IY}\r\n\r\nAF'  {AFp}\r\nBC'  {BCp}\r\nDE'  {DEp}\r\nHL'  {HLp}\r\n\r\nIR   {I}{R}\r\nWZ   {WZ}\r\n\r\n(HL) {HLM}\r\n(SP) {SPM}";
        
        // REALTIME DISASSEMBLY

        public string GetRealtimeDisassembly() => HistoricDisassemblyMode ? GetRealtimeDisassemblyHistoric() : GetRealtimeDisassemblyNormal();
        public string GetRealtimeDisassemblyNormal()
        {
            ushort startLocation = PC.Value;

            const int MAX_HIGHLIGHT_LINE = NUM_DISASSEMBLY_LINES - 4;

            int idx;
            if ((idx = Array.IndexOf(disassemblyAddresses, startLocation)) > 0)
                startLocation = disassemblyAddresses[(idx > MAX_HIGHLIGHT_LINE) ? MAX_HIGHLIGHT_LINE : 0];

            int j = 0;

            return string.Join(Environment.NewLine,
                               disassemblyAddresses
                                         .Select(i =>
                                            new { addr = disassemblyAddresses[j++] = startLocation })
                                                .Select(n => GetLineInfo((n.addr == PC.Value) ? ">" : " ", ref startLocation, GetInstructionAt(n.addr))));
        }
        public string GetRealtimeDisassemblyHistoric()
        {
            return string.Join(Environment.NewLine,
                               historyBuffer.Select(i => new { addr = i, inst = GetInstructionAt(i) })
                                            .Select(n => string.Format("{0}{1} {2} {3}",
                                                                        (PC.Value == n.addr) ? ">" : " ",
                                                                        n.addr.ToHexString(),
                                                                        Lib.GetSpacedHex(Memory, n.addr, n.inst.Size),
                                                                        n.inst.FullName(Memory, n.addr))));
        }
        internal string GetLineInfo(string Prefix, ref ushort PC, Instruction inst)
        {
            var s = string.Format("{0}{1}  {2} {3}", Prefix, PC.ToHexString(), Lib.GetSpacedHex(Memory, PC, inst.Size), inst.FullName(Memory, PC));
            PC += inst.Size;
            return s;
        }

        // INSTRUCTION SET

        public string GetInstructionSetReport() => InstructionSet.GetInstructionSetReport();

        // ISTATUS IMPLEMENTATION

        public ushort PcVal => PC.Value;
        public ushort SpVal => SP.Value;

        public byte AVal => A.Value;

        public ushort AfVal => AF.Value;
        public ushort BcVal => BC.Value;
        public ushort DeVal => DE.Value;
        public ushort HlVal => HL.Value;

        public ushort IxVal => IX.Value;
        public ushort IyVal => IY.Value;

        public ushort AfpVal => AFp.Value;
        public ushort BcpVal => BCp.Value;
        public ushort DepVal => DEp.Value;
        public ushort HlpVal => HLp.Value;

        public byte HlmVal => HLM.Value;

        public ushort WzVal => WZ.Value;
        public ushort IrVal => (ushort)((I.Value << 8) | R.Value);

        public string Flags => flagsToString[F.Value];
    }
}
