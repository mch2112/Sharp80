 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.Z80
{
    internal class TraceLog
    {
        private const int MAX_LINES = 1000000;
        private Z80 cpu;
        private List<string> trace;

        private ushort pc;
        private byte a;
        private ushort bc;
        private ushort de;
        private ushort hl;
        private ushort ix;
        private ushort iy;
        private ushort sp;
        private byte hlm;
        private string flags;

        private int lineCount;

        private Object logLock = new Object();

        public TraceLog(Z80 Cpu, ulong ElapsedTStataes)
        {
            cpu = Cpu;
            trace = new List<string>(100000);
        }
        public ushort Log(ulong ElapsedTStates, Func<Instruction, ushort> Exec)
        {
            var i = cpu.CurrentInstruction;

            string inst;

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

            inst += $"{ElapsedTStates:000,000,000}   {cpu.PcVal:X4}  {i.FullName(cpu.Memory, cpu.PcVal)}".PadRight(34);

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


            var retVal = Exec(i);

            //if (pc == cpu.PcVal) inst += "        "; else inst += $"PC {cpu.PcVal:X4} ";
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
            var t = trace;
            lock (logLock)
            {
                var ret = String.Join(Environment.NewLine, trace);
                trace.Clear();
                return ret;
            }
        }
    }
}
