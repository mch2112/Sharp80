/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

using Sharp80.TRS80;

namespace Sharp80.Views
{
    internal class ViewAssembler : View
    {
        protected override ViewMode Mode => ViewMode.Assembler;
        protected override bool CanSendKeysToEmulation => false;
        protected override bool ForceRedraw => false;
        protected override bool processKey(KeyState Key)
        {
            if (Key.IsUnmodified && Key.Pressed)
            {
                switch (Key.Key)
                {
                    case KeyCode.R:
                        if (InvokeAssembler(true))
                        {
                            Computer.LoadCMDFile(CmdFile);
                            Computer.Start();
                            RevertMode();
                        }
                        return true;
                    case KeyCode.L:
                        if (InvokeAssembler(false))
                            CurrentMode = ViewMode.CmdFile;
                        break;
                }
            }
            return base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                                Header("Z80 Assembler") +
                                Format() +
                                Indent($"{ProductInfo.ProductName} can assemble your Z80 .asm files.") +
                                Format() +
                                Indent("[R] Assemble .asm file and run") +
                                Format() +
                                Indent("[L] Assemble and load to memory only")
                                ));
        }
    }
}