using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
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
                        if (InvokeAssembler())
                        {
                            Computer.LoadCMDFile(CmdFile);
                            Computer.Start();
                            RevertMode();
                        }
                        return true;
                    case KeyCode.L:
                        if (InvokeAssembler())
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
                                Format() +
                                Indent("[R] Assemble .asm file and Run.") +
                                Format() +
                                Indent("[L] Assemble and Load to memory.")
                                ));
        }
    }
}
