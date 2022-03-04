﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;

namespace Sharp80.TRS80
{
    public interface IMemory : IReadOnlyList<byte>, ISerializable
    {
        byte this[ushort Location] { get; set; }

        ArraySegment<byte> VideoMemory { get; }

        ushort GetWordAt(ushort Address);
        void SetWordAt(ushort Address, ushort Value);

        bool AltKeyboardLayout { get; set; }
        bool NotifyKeyboardChange(KeyState Key);
        void ResetKeyboard(bool LeftShift, bool RightShift);
    }
}
