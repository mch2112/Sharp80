/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

namespace Sharp80
{
    internal class ViewSplash : View
    {
        protected override ViewMode Mode => ViewMode.Splash;
        protected override bool ForceRedraw => false;
        protected override bool CanSendKeysToEmulation => false;

        protected override byte[] GetViewBytes()
        {
            return PadScreen(Encoding.ASCII.GetBytes(
                                Separator() +
                                Center($"{ProductInfo.PRODUCT_NAME} - TRS-80 Model III Emulator") +
                                Center(string.Format($"Version {System.Windows.Forms.Application.ProductVersion}  (c) {ProductInfo.PRODUCT_AUTHOR} {DateTime.Now.Year}")) +
                                Separator() +
                                Format() + 
                                Indent("[F8] Start Emulator") +
                                Format() +
                                Indent("[F1] Command Help") +
                                Indent("[F2] Options") +
                                Indent("[F3] Floppy Disk Manager") +
                                Indent("[F4] Tape Manager") +
                                Format() +
                                Format() +
                                Format() +
                                Footer($"Visit {ProductInfo.PRODUCT_URL} for more information.")
                                ));
        }
        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed && Key.IsUnmodified)
            {
                switch (Key.Key)
                {
                    case KeyCode.F8:
                    case KeyCode.F9:
                        // Note: doesn't consume key event
                        if (!Computer.IsRunning)
                            CurrentMode = ViewMode.Normal;
                        break;
                }
            }
            return base.processKey(Key);
        }
    }
}
