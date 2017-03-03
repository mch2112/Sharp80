using System.Collections.Generic;

namespace Sharp80
{
    /// <summary>
    /// This interface is used to allow for future non-DirectInput based implementations
    /// </summary>
    internal interface IKeyboard : IEnumerable<KeyState>
    {
        bool IsShifted { get; }
        bool LeftShiftPressed { get; }
        bool RightShiftPressed { get; }
        bool IsControlPressed { get; }
        bool IsAltPressed { get; }

        bool IsDisposed { get; }
        
        void Dispose();        
        void Refresh();
    }
}