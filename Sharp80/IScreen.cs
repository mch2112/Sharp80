/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.IO;

namespace Sharp80
{
    internal interface IScreen : ISerializable, IDisposable
    {
        bool IsFullScreen { get; set; }
        bool AdvancedView { get; }
        string StatusMessage { set; }
        bool IsDisposed { get; }

        void Render();
        void Reset();
        void SetVideoMode(bool? WideCharMode, bool? KanjiCharMode);
        void Initialize(IAppWindow Parent);
        void Reinitialize(Computer Computer);

    }
}
