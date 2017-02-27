using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class ViewBreakpoint : View
    {
        protected override ViewMode Mode => ViewMode.SetBreakpointView;
        protected override bool ForceRedraw => false;
        protected override bool processKey(KeyState Key)
        {
            if (Key.Released)
                return base.processKey(Key);

            char c = '\0';
            switch (Key.Key)
            {
                case KeyCode.Space:
                    Computer.Processor.BreakPointOn = !Computer.Processor.BreakPointOn;
                    Invalidate();
                    return true;
                case KeyCode.Return:
                    CurrentMode = ViewMode.NormalView;
                    return true;
                default:
                    c = Key.ToChar();
                    break;
            }

            bool processed = false;
            if (Computer.Processor.BreakPoint.RotateAddress(c, out ushort newBp))
            {
                Computer.Processor.BreakPoint = newBp;
                Invalidate();
                processed = true;
            }
            return processed || base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Set breakpoint location") +
                Format() +
                Indent("Breakpoint is currently " + (Computer.Processor.BreakPointOn ? "ON" : "OFF")) +
                Indent("Breakpoint Value (Hexadecimal): " + Computer.Processor.BreakPoint.ToHexString()) +
                Format() +
                Separator() +
                Indent("Type [0]-[9] or [A]-[F] to enter a hexadecimal") +
                Indent("breakpoint location.") +
                Format() +
                Indent("[Space Bar] to toggle breakpoint on and off.") +
                Format() +
                Indent("[Enter] when done.")));
        }
    }
}
