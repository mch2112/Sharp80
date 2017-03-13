/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Windows.Forms;

namespace Sharp80
{
    /// <summary>
    /// Helper class to help process user initiated window sizing
    /// events and maintain the proper aspect ratio.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        public const int WM_SIZING   = 0x214;
        public const int WMSZ_LEFT   = 1;
        public const int WMSZ_RIGHT  = 2;
        public const int WMSZ_TOP    = 3;
        public const int WMSZ_BOTTOM = 6;

        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public Message Message { get; private set; }
        public MessageEventArgs(Message Message)
        {
            System.Diagnostics.Debug.Assert(Message.Msg == WM_SIZING);
            this.Message = Message;
        }
    }
}
