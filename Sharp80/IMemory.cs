using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal interface IMemory : ISerializable
    {
        byte this[ushort Location] { get; set; }

        ushort GetWordAt(ushort Address);
        void SetWordAt(ushort Address, ushort Value);

        bool ScreenWritten { get; set; }
        void NotifyKeyboardChange(SharpDX.DirectInput.Key Key, bool IsPressed);
        void ResetKeyboard();
    }
}
