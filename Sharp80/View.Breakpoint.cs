﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Text;

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
                    Computer.BreakPointOn = !Computer.BreakPointOn;
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
            if (Computer.BreakPoint.RotateAddress(c, out ushort newBp))
            {
                Computer.BreakPoint = newBp;
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
                Indent("Breakpoint is currently " + (Computer.BreakPointOn ? "ON" : "OFF")) +
                Indent("Breakpoint Value (Hexadecimal): " + Computer.BreakPoint.ToHexString()) +
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