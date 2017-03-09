/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;

namespace Sharp80
{
    /// <summary>
    /// This interface is used to allow for future non-DirectInput based implementations
    /// </summary>
    internal interface IKeyboard : IEnumerable<KeyState>, IDisposable
    {
        bool IsShifted { get; }
        bool LeftShiftPressed { get; }
        bool RightShiftPressed { get; }
        bool IsControlPressed { get; }
        bool IsAltPressed { get; }

        bool IsDisposed { get; }
             
        void Refresh();
    }
}