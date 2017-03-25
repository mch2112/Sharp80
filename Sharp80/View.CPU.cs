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
        protected override bool CanSendKeysToEmulation => false;
        protected override bool processKey(KeyState Key)
        {
            Invalidate();
            return base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            var Status = Computer.CpuStatus;

            return PadScreen(Encoding.ASCII.GetBytes(
                Header($"{ProductInfo.PRODUCT_NAME} Z80 Register Status") +
                Format() +
                Indent($"PC  {Status.PcVal:X4}  SP  {Status.SpVal:X4}") +
                Format() +
                Indent($"AF  {Status.AfVal:X4}  AF' {Status.AfpVal:X4}") +
                Indent($"BC  {Status.BcVal:X4}  BC' {Status.BcpVal:X4}") +
                Indent($"DE  {Status.DeVal:X4}  DE' {Status.DepVal:X4}") +
                Indent($"HL  {Status.HlVal:X4}  HL' {Status.HlpVal:X4}") +
                Format() +
                Indent($"IX  {Status.IxVal:X4}  IY' {Status.IyVal:X4}")));
        }
    }
}
