using System;

namespace Sharp80
{
    internal interface IScreen : ISerializable
    {
        void Invalidate();
        void Initialize(Computer Computer);
        void SetVideoMode(bool IsWide, bool IsKanji);
    }
}
