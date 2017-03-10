﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal interface IMemory : ISerializable
    {
        byte this[ushort Location] { get; set; }

        ushort GetWordAt(ushort Address);
        void SetWordAt(ushort Address, ushort Value);

        bool AltKeyboardLayout { get; set; }
        bool ScreenWritten { get; set; }
        bool NotifyKeyboardChange(KeyState Key);
        void ResetKeyboard();
    }
}
