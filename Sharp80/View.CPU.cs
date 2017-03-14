/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80
{
    internal class ViewCpu : View
    {
        protected override ViewMode Mode => ViewMode.Cpu;
        protected override bool ForceRedraw => Computer.IsRunning;
        protected override byte[] GetViewBytes()
        {
            var Status = Computer.CpuStatus;

            Invalidate();
            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 Z80 Register Status") +
                Format() +
                Indent(string.Format("PC  {0}  SP  {1}", Status.PcVal.ToHexString(), Status.SpVal.ToHexString())) +
                Format() +
                Indent(string.Format("AF  {0}  AF' {1}", Status.AfVal.ToHexString(), Status.AfpVal.ToHexString())) +
                Indent(string.Format("BC  {0}  BC' {1}", Status.BcVal.ToHexString(), Status.BcpVal.ToHexString())) +
                Indent(string.Format("DE  {0}  DE' {1}", Status.DeVal.ToHexString(), Status.DepVal.ToHexString())) +
                Indent(string.Format("HL  {0}  HL' {1}", Status.HlVal.ToHexString(), Status.HlpVal.ToHexString())) +
                Format() +
                Indent(string.Format("IX  {0}  IY  {1}", Status.IxVal.ToHexString(), Status.IyVal.ToHexString()))));
        }
    }
}
