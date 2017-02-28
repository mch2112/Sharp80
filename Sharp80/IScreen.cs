/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.IO;

namespace Sharp80
{
    internal interface IScreen : ISerializable
    {
        void Reset();
        void SetVideoMode(bool WideCharMode, bool KanjiCharMode);
    }
}
