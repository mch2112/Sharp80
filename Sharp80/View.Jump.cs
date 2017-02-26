using System;
using System.Text;

namespace Sharp80
{
    internal class ViewJump : View
    {
        protected override bool ForceRedraw => false;
        protected override ViewMode Mode => ViewMode.JumpToView;

        protected override void Activate()
        {
            Computer.Stop(true);
            MessageCallback("Jump To View On");
        }

        protected override bool processKey(KeyState Key)
        {
            if (Key.Released)
                return base.processKey(Key);

            char c = '\0';
            bool processed = false;
            switch (Key.Key)
            {
                case KeyCode.Return:
                    CurrentMode = ViewMode.NormalView;
                    return true;
                case KeyCode.F8:
                    return true;
                default:
                    c = Key.ToChar();
                    break;
            }
            if (c != '\0')
            {
                string addressString = Lib.ToHexString(Computer.Processor.PC.val);
                addressString = addressString + c;
                if (addressString.Length > 4)
                    addressString = addressString.Substring(addressString.Length - 4, 4);

                if (ushort.TryParse(addressString,
                                    System.Globalization.NumberStyles.AllowHexSpecifier,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out ushort addr))
                {
                    Computer.Processor.Jump(addr);
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
                                Indent("Jump to memory location (Hexadecimal): " + Lib.ToHexString(Computer.Processor.PC.val)) +
                                Format() +
                                Separator() +
                                Indent("Type [0]-[9] or [A]-[F] to enter a hexadecimal") +
                                Indent("jump location.") +
                                Format() +
                                Indent("[Escape] when done.")));
        }
    }
}
