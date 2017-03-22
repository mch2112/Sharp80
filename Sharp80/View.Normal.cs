/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class ViewNormal : View
    {
        protected override bool ForceRedraw => true;
        protected override ViewMode Mode => ViewMode.Normal;
        protected override bool CanSendKeysToEmulation => true;

        private bool pasting;

        protected override bool processKey(KeyState Key)
        {
            switch (Key.Key)
            {
                case KeyCode.Escape:
                    if (pasting)
                    {
                        pasting = false;
                        break;
                    }
                    else
                    {
                        return Computer.NotifyKeyboardChange(Key);
                    }
                case KeyCode.V:
                    if (Key.Released && Key.Control && !Key.Alt)
                    {
                        Paste();
                        return true;
                    }
                    break;
            }
            return base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            return null;
        }
        private async void Paste()
        {
            string text = Dialogs.ClipboardText;

            if (text.Length == 0)
            {
                MessageCallback("No text on clipboard");
            }
            else
            {
                pasting = true;
                foreach (char c in text)
                {
                    Computer.NotifyKeyboardChange(new KeyState(c, true));
                    if (c == '\n')
                        await Task.Delay(200);
                    else
                        await Task.Delay(40);
                    Computer.NotifyKeyboardChange(new KeyState(c, false));
                    if (!pasting)
                        break;
                    if (c == '\n')
                        await Task.Delay(1000);
                    else
                        await Task.Delay(40);

                    MessageCallback("Pasting - [Esc] to cancel");
                }
                MessageCallback("Paste Done.");
            }
        }
    }
}
