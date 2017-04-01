/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;

using Sharp80.TRS80;

namespace Sharp80.Views
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
                                Center($"{ProductInfo.ProductName} - TRS-80 Model III Emulator") +
                                Center(string.Format($"Version {ProductInfo.ProductVersion}  (c) {ProductInfo.ProductAuthor} {DateTime.Now.Year}")) +
                                Separator() +
                                Format() +
                                Indent("[F8] Start Emulator") +
                                Format() +
                                Indent("[F1] Command Help") +
                                Indent("[F2] Options") +
                                Indent("[F3] Floppy Disk Manager") +
                                Indent("[F4] Tape Manager") +
                                Format() +
                                Indent("[F12] Toggle Normal / Fast emulation speed") +
                                Format() +
                                Footer($"Visit {ProductInfo.ProductURL} for more information.")
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
