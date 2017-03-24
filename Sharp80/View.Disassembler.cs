/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Text;

namespace Sharp80
{
    internal class ViewDisassembler : View
    {
        protected override ViewMode Mode => ViewMode.Disassembler;
        protected override bool ForceRedraw => false;
        protected override bool CanSendKeysToEmulation => false;

        private ushort startAddress = 0;
        private bool makeAssemblable = false;
        private bool lowercase = false;

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
                    case KeyCode.L:
                        lowercase = !lowercase;
                        processed = true;
                        break;
                    case KeyCode.M:
                        makeAssemblable = !makeAssemblable;
                        processed = true;
                        break;
                    case KeyCode.S:
                        Disassemble();
                        processed = true;
                        break;
                    default:
                        c = Key.ToHexChar();
                        break;
                }
                if (startAddress.RotateAddress(c, out ushort addr))
                {
                    startAddress = addr;
                    processed = true;
                }
            }
            Invalidate();
            return processed || base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                                Header("Z80 Disassembler") +
                                Format() +
                                Indent($"Disassembly start address (hex): {startAddress:X4}") +
                                Format() +
                                Separator() +
                                Indent("Type [0]-[9] or [A]-[F] to change the start location.") +
                                Format() +
                                Format() +
                                Indent("[S] Start disassembly")  +
                                Format() +
                                Indent("[M] Make reassemblable (strip opcode info): " + (makeAssemblable ? "[ON] /  OFF" : " ON  / [OFF]")) +
                                Indent("[L] Lower case output:                      " + (lowercase ?       "[ON] /  OFF" : " ON  / [OFF]"))
                                ));
        }
        private void Disassemble()
        {
            var txt = Computer.Disassemble(startAddress, 0xFFFF, makeAssemblable);
            if (lowercase)
                txt = txt.ToLower();
            var path = Path.Combine(Storage.AppDataPath, "Disassembly.txt").MakeUniquePath();
            Storage.SaveTextFile(path, txt);
            InvokeUserCommand(UserCommand.Window);
            Dialogs.ShowTextFile(path);
        }
    }
}
