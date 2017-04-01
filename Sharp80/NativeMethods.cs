/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Runtime.InteropServices;

namespace Sharp80
{
    internal static class NativeMethods
    {
#pragma warning disable IDE1006 // Naming Styles
        [DllImport("user32.dll")]
        static internal extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
#pragma warning restore IDE1006 // Naming Styles
    }
}
