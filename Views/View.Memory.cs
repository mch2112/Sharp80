/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using Sharp80.TRS80;

namespace Sharp80.Views
{
    internal class ViewMemory : View
    {
        protected override ViewMode Mode => ViewMode.Memory;
        protected override bool ForceRedraw => true;
        protected override bool CanSendKeysToEmulation => baseAddress == 0x3800;

        private ushort baseAddress = 0;

        protected override void Activate()
        {
            MessageCallback("Memory View: Arrow Keys to Page");
            base.Activate();
        }
        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed && Key.IsUnmodified)
            {
                switch (Key.Key)
                {
                    case KeyCode.Up:
                    case KeyCode.PageUp:
                        baseAddress -= 0x1000;
                        Invalidate();
                        break;
                    case KeyCode.Left:
                        baseAddress -= 0x0100;
                        Invalidate();
                        break;
                    case KeyCode.Down:
                    case KeyCode.PageDown:
                        baseAddress += 0x1000;
                        Invalidate();
                        break;
                    case KeyCode.Right:
                        baseAddress += 0x0100;
                        Invalidate();
                        break;
                    default:
                        return base.processKey(Key);
                }
                return true;
            }
            else
            {
                return base.processKey(Key);
            }
        }
        protected override byte[] GetViewBytes()
        {
            byte[] cells = new byte[ScreenMetrics.NUM_SCREEN_CHARS];

            int cell = 0;
            int rawCell = 0x30;

            for (int k = 0; k < 0x100; k++)
            {
                if ((k & 0x0F) == 0x00)
                {
                    ushort lineAddress = (ushort)(baseAddress + k);
                    cells[cell++] = ((lineAddress >> 12) & 0x0F).ToHexCharByte();
                    cells[cell++] = ((lineAddress >> 8) & 0x0F).ToHexCharByte();
                    cells[cell++] = ((lineAddress >> 4) & 0x0F).ToHexCharByte();
                    cells[cell++] = ((lineAddress) & 0x0F).ToHexCharByte();
                    cell += 2;
                }

                byte b = Computer.Memory[(ushort)(baseAddress + k)];

                WriteToByteArrayHex(cells, cell, b);
                cell += 2;

                if (k % 2 == 1)
                    cell++;

                cells[rawCell++] = b;

                if ((k & 0x0F) == 0x0F)
                {
                    // wrap to new line on screen
                    rawCell += 0x30;
                    cell += 0x20 - 14;
                }
            }
            return cells;
        }
    }
}
