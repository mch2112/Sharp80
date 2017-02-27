/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Text;

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
                Indent(string.Format("PC  {0}  SP  {1}", Status.PC.ToHexString(), Status.SP.ToHexString())) +
                Format() +
                Indent(string.Format("AF  {0}  AF' {1}", Status.AF.ToHexString(), Status.AFp.ToHexString())) +
                Indent(string.Format("BC  {0}  BC' {1}", Status.BC.ToHexString(), Status.BCp.ToHexString())) +
                Indent(string.Format("DE  {0}  DE' {1}", Status.DE.ToHexString(), Status.DEp.ToHexString())) +
                Indent(string.Format("HL  {0}  HL' {1}", Status.HL.ToHexString(), Status.HLp.ToHexString())) +
                Format() +
                Indent(string.Format("IX  {0}  IY  {1}", Status.IX.ToHexString(), Status.IY.ToHexString()))));
        }
    }
}
