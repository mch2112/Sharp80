using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class CPUView : View
    {
        protected override ViewMode Mode => ViewMode.CpuView;
        protected override bool ForceRedraw => Computer.IsRunning;
        protected override byte[] GetViewBytes()
        {
            var Status = Computer.Processor.GetStatus();

            Invalidate();
            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Z80 Register Status") +
                Format() +
                Indent(string.Format("PC  {0}  SP  {1}", Lib.ToHexString(Status.PC), Lib.ToHexString(Status.SP))) +
                Format() +
                Indent(string.Format("AF  {0}  AF' {1}", Lib.ToHexString(Status.AF), Lib.ToHexString(Status.AFp))) +
                Indent(string.Format("BC  {0}  BC' {1}", Lib.ToHexString(Status.BC), Lib.ToHexString(Status.BCp))) +
                Indent(string.Format("DE  {0}  DE' {1}", Lib.ToHexString(Status.DE), Lib.ToHexString(Status.DEp))) +
                Indent(string.Format("HL  {0}  HL' {1}", Lib.ToHexString(Status.HL), Lib.ToHexString(Status.HLp))) +
                Format() +
                Indent(string.Format("IX  {0}  IY  {1}", Lib.ToHexString(Status.IX), Lib.ToHexString(Status.IY)))));
        }
    }
}
