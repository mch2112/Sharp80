/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Text;
using System.Threading;
using Sharp80.TRS80;

namespace Sharp80.Views
{
    internal class ViewNormal : View
    {
        protected override bool ForceRedraw => true;
        protected override ViewMode Mode => ViewMode.Normal;
        protected override bool CanSendKeysToEmulation => true;

        private CancellationTokenSource PasteCancelToken = null;

        protected override bool processKey(KeyState Key)
        {
            switch (Key.Key)
            {
                case KeyCode.Escape:
                    if (PasteCancelToken != null)
                    {
                        PasteCancelToken.Cancel();
                        PasteCancelToken = null;
                        break;
                    }
                    else
                    {
                        return Computer.NotifyKeyboardChange(Key);
                    }
                case KeyCode.C:
                    if (Key.Released && Key.Control && !Key.Alt)
                    {
                        Copy();
                        return true;
                    }
                    break;
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
        private void Copy()
        {
            var m = Computer.VideoMemory;
            var blank = true;
            StringBuilder sb = new StringBuilder();
            var inc = Computer.WideCharMode ? 2 : 1;
            for (int i = 0; i < ScreenMetrics.NUM_SCREEN_CHARS_Y; i++)
            {
                for (int j = 0; j < ScreenMetrics.NUM_SCREEN_CHARS_X; j += inc)
                {
                    var b = m[i * ScreenMetrics.NUM_SCREEN_CHARS_X + j];

                    if (b.IsBetween(0x21, 0x7F))
                    {
                        sb.Append((char)b);
                        blank = false;
                    }
                    else if (b >= 0x80)
                    {
                        sb.Append('.');
                        blank = false;
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(Environment.NewLine);
            }
            if (blank)
            {
                Dialogs.InformUser("Screen is blank.");
            }
            else
            {
                Dialogs.ClipboardText = sb.ToString();
                Dialogs.InformUser("Screen copied to Windows clipboard.");
            }
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
                PasteCancelToken = new CancellationTokenSource();
                MessageCallback("&Pasting text. [Esc] to cancel.");
                await Computer.Paste(text, PasteCancelToken.Token);
                MessageCallback("Paste Done.");
            }
        }
    }
}
