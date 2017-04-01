using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    public class ScreenNull : IScreen
    {
        private byte[] shadowScreen = new byte[ScreenMetrics.NUM_SCREEN_CHARS];
        private Computer computer;

        public async Task Start(float RefreshRateHz, CancellationToken StopToken)
        {
            await Task.Delay(1);
        }

        public bool Suspend { set { } }

        public IList<byte> ScreenBytes => shadowScreen;
        public bool IsFullScreen { get; set; }
        public bool AdvancedView { get; }
        public bool WideCharMode { get; set; }
        public bool AltCharMode { get; set; }
        public string StatusMessage { set { } }

        public void Reset() { }
        //public void Initialize(IAppWindow Parent) { }
        public void Initialize(Computer Computer) { computer = Computer; }

        public bool Deserialize(BinaryReader Reader, int SerializationVersion)
        {
            throw new NotImplementedException();
        }
        public void Serialize(BinaryWriter Writer)
        {
            throw new NotImplementedException();
        }
        public void Dispose() { }
    }
}
