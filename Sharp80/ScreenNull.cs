using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80
{
    public class ScreenNull : IScreen
    {
        private byte[] shadowScreen = new byte[ScreenMetrics.NUM_SCREEN_CHARS];
        private Computer computer;

        public async Task Start(float RefreshRateHz, CancellationToken StopToken)
        {
            var delay = TimeSpan.FromTicks((int)(10_000_000f / RefreshRateHz));
            await RenderLoop(delay, StopToken);
        }
        private async Task RenderLoop(TimeSpan Delay, CancellationToken StopToken)
        {
            while (!StopToken.IsCancellationRequested)
            {
                int i = 0;

                foreach (var b in computer.VideoMemory)
                    shadowScreen[i] = b;

                await Task.Delay(Delay, StopToken);
            }
        }

        public bool Suspend { set { } }

        public IList<byte> ScreenBytes => shadowScreen;
        public bool IsFullScreen { get; set; }
        public bool AdvancedView { get; }
        public bool WideCharMode { get; set; }
        public bool AltCharMode { get; set; }
        public string StatusMessage { set { } }

        public void Reset() { }
        public void Initialize(IAppWindow Parent) { }
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
