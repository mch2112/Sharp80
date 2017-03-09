/// Sharp 80 (c) Matthew Hamilton~
/// Licensed Under GPL v3

using System;
using System.Text;

namespace Sharp80
{
    internal class ViewJump : View
    {
        protected override bool ForceRedraw => false;
        protected override ViewMode Mode => ViewMode.Jump;

        protected override void Activate()
        {
            Computer.Stop(true);
            MessageCallback("Jump View On");
        }

        protected override bool processKey(KeyState Key)
        {
            if (Key.Released)
                return base.processKey(Key);

            bool processed = false;

            if (Key.IsUnmodified)
            {
                char c = '\0';
                switch (Key.Key)
                {
                    case KeyCode.Return:
                        RevertMode();
                        return true;
                    case KeyCode.F8:
                        CurrentMode = ViewMode.Normal;
                        return false;
                    default:
                        c = Key.ToHexChar();
                        break;
                }
                if (Computer.ProgramCounter.RotateAddress(c, out ushort newPc))
                {
                    Computer.Jump(newPc);
                    Invalidate();
                    processed = true;
                }
            }
            return processed || base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                                Header("Jump to Z80 memory location") +
                                Format() +
                                Indent("Jump to memory location (Hexadecimal): " + Computer.ProgramCounter.ToHexString()) +
                                Format() +
                                Separator() +
                                Indent("Type [0]-[9] or [A]-[F] to enter a hexadecimal") +
                                Indent("jump location.") +
                                Format() +
                                Indent("[Escape] when done.")));
        }
    }
}
