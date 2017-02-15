using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sharp80
{
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
        public MessageEventArgs(Message M)
        {
            System.Diagnostics.Debug.Assert(M.Msg == WM_SIZING);
            this.Message = M;
        }
    }
}
