/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;

namespace Sharp80.Z80
{
    internal class TraceLog
    {
        private const int MAX_LINES = 1000000;
        private Z80 cpu;
        private List<string> trace;
        private ushort pc, bc, de, hl, ix, iy, sp;
        private byte a, hlm;
        private string flags;
        private int lineCount;
        private Object logLock = new Object();

        public TraceLog(Z80 Cpu, ulong ElapsedTStataes)
        {
            cpu = Cpu;
            trace = new List<string>(100000);
        }


        /// <summary>
        /// Each line has two parts: the left part shows the instruction about to be executed
        /// and the right shows the state after execution. This requires executing the instruction
        /// from within this method.
        /// </summary>
        public ushort Log(ulong ElapsedTStates, Func<Instruction, ushort> Exec)
        {
            var i = cpu.CurrentInstruction;
            string inst;

            // Every 20 instructions, show a complete snapshot of the register status
            if (++lineCount % 20 == 0)
            {
                inst = "=======================================================================================================" + Environment.NewLine + 
                       $"{ElapsedTStates:000,000,000}                       A:{a:X2} HL:{hl:X4} BC:{bc:X4} DE:{de:X4} IX:{ix:X4} IY:{iy:X4} SP:{sp:X4} (HL):{hlm:X2} {flags}" + Environment.NewLine +
                       "=======================================================================================================" + Environment.NewLine;
            }
            else
            {
                inst = "";
            }

            // Show instruction about to be executed
            inst += $"{ElapsedTStates:000,000,000}   {cpu.PcVal:X4}  {i.FullName(cpu.Memory, cpu.PcVal)}".PadRight(34);

            // Capture register values so we know if they've changed
            pc = cpu.PcVal;
            a = cpu.AVal;
            bc = cpu.BcVal;
            de = cpu.DeVal;
            hl = cpu.HlVal;
            ix = cpu.IxVal;
            iy = cpu.IyVal;
            sp = cpu.SpVal;
            hlm = cpu.HlmVal;
            flags = cpu.Flags;

            // Execute the current instruction
            var retVal = Exec(i);

            // Show changed registers
            if (a == cpu.AVal) inst += "     ";      else inst += $"A:{cpu.AVal:X2} ";
            if (hl == cpu.HlVal) inst += "        "; else inst += $"HL:{cpu.HlVal:X4} ";
            if (bc == cpu.BcVal) inst += "        "; else inst += $"BC:{cpu.BcVal:X4} ";
            if (de == cpu.DeVal) inst += "        "; else inst += $"DE:{cpu.DeVal:X4} ";
            if (ix == cpu.IxVal) inst += "        "; else inst += $"IX:{cpu.IxVal:X4} ";
            if (iy == cpu.IyVal) inst += "        "; else inst += $"IY:{cpu.IyVal:X4} ";
            if (sp == cpu.SpVal) inst += "        "; else inst += $"SP:{cpu.SpVal:X4} ";
            if (hlm == cpu.HlmVal) inst += "        "; else inst += $"(HL):{cpu.HlmVal:X2} ";
            if (flags != cpu.Flags) inst += cpu.Flags;

            inst = inst.TrimEnd();
            lock (logLock)
            {
                if (trace.Count > MAX_LINES * 11 / 10)
                    trace.RemoveRange(0, MAX_LINES / 10);
                trace.Add(inst);
            }
            return retVal;
        }
        public void AddToLog(ulong ElapsedTStates, string Item)
        {
            lock (logLock)
            {
                trace.Add($"{ElapsedTStates:000,000,000} " + Item);
            }
        }
        public string GetLogAndClear()
        {
            lock (logLock)
            {
                var ret = String.Join(Environment.NewLine, trace);
                trace.Clear();
                return ret;
            }
        }
    }
}
