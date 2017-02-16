using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class Screen : ISerializable
    {
        public bool WideCharMode { get; set; } = false;
        public bool KanjiCharMode { get; set; } = false;

        private ScreenDX PhysicalScreen { get; set;}

        public Screen(ScreenDX PhysicalScreen)
        {
            this.PhysicalScreen = PhysicalScreen;
        }
        public void Reset()
        {
            PhysicalScreen.Invalidate();
            SetVideoMode();
        }
        public void SetVideoMode(bool WideCharMode, bool KanjiCharMode)
        {
            this.WideCharMode = WideCharMode;
            this.KanjiCharMode = KanjiCharMode;
            SetVideoMode();
        }
        public void SetVideoMode()
        {
            PhysicalScreen.SetVideoMode(WideCharMode, KanjiCharMode);
        }
        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(WideCharMode);
            Writer.Write(KanjiCharMode);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            SetVideoMode(Reader.ReadBoolean(), Reader.ReadBoolean());
            PhysicalScreen.Reset();
        }
    }
}
