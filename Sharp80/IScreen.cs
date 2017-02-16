using System;

namespace Sharp80
{
    internal interface IScreen
    {
        void Invalidate();
        void Reset();
        void Initialize(Computer Computer);
        void SetVideoMode(bool IsWide, bool IsKanji);
    }
}
