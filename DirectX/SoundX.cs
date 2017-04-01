/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;

using SharpDX;
using SharpDX.Multimedia;
using SharpDX.XAudio2;

using Sharp80.TRS80;

namespace Sharp80.DirectX
{
    public partial class SoundX : ISound, IDisposable
    {
        public int SampleRate { get; private set; }

        private static bool AnyInitFail { get; set; } = false;
        public bool Stopped { get; private set; }

        private XAudio2 xaudio;
        private MasteringVoice masteringVoice;
        private SourceVoice sourceVoice;
        private Task playingTask;
        private AutoResetEvent bufferEndEvent;

        private const int FRAMES_PER_SECOND = 20;
        private const int BITS_PER_SAMPLE = 16;
        private const int BYTES_PER_SAMPLE = BITS_PER_SAMPLE / 8;
        private const int CHANNELS = 1; // glorious TRS-80 mono
        private const int RING_SIZE = 2;

        private const short MAX_SOUND_AMPLITUDE = 0x40 * 0x100;
        private const short NULL_SOUND_LEVEL = 0x0;
        private const short MIN_SOUND_LEVEL = NULL_SOUND_LEVEL - MAX_SOUND_AMPLITUDE;
        private const short MAX_SOUND_LEVEL = NULL_SOUND_LEVEL + MAX_SOUND_AMPLITUDE;

        private readonly short[] outTable = new short[] { NULL_SOUND_LEVEL, MAX_SOUND_LEVEL, MIN_SOUND_LEVEL, NULL_SOUND_LEVEL };

        private Noise noise;

        private bool on = false;
        private bool mute = false;
        private bool enabled = true;
        private bool isDisposed = false;

        public bool On
        {
            get { return on; }
            set
            {
                if (on != value)
                {
                    on = value;
                    UpdateEnabled();
                }
            }
        }
        public bool Mute
        {
            get => mute;
            set
            {
                if (mute != value)
                {
                    mute = value;
                    UpdateEnabled();
                }
            }
        }
        public bool UseDriveNoise { get; set; } = false;
        public bool DriveMotorRunning { get; set; } = false;

        public void TrackStep() => noise.TrackStep();

        private int ringCursor = 0;

        private AudioBuffer[] audioBuffersRing = new AudioBuffer[RING_SIZE];
        private DataPointer[] memBuffers = new DataPointer[RING_SIZE];
        private FrameBuffer<short> frameBuffer;

        public SampleCallback SampleCallback { private get; set; }

        public SoundX(int SampleRate)
        {
            this.SampleRate = SampleRate;

            Stopped = true;

            if (AnyInitFail)
                return;

            try
            {
                frameBuffer = new FrameBuffer<short>(SampleRate / FRAMES_PER_SECOND * CHANNELS, SampleRate / 20);
                noise = new Noise(SampleRate, MAX_SOUND_AMPLITUDE);

                xaudio = new XAudio2();

                masteringVoice = new MasteringVoice(xaudio, CHANNELS, SampleRate);

                bufferEndEvent = new AutoResetEvent(false);

                var frameSizeBytes = SampleRate / FRAMES_PER_SECOND * CHANNELS * 2;

                for (int i = 0; i < RING_SIZE; i++)
                {
                    audioBuffersRing[i] = new AudioBuffer()
                    {
                        AudioBytes = frameSizeBytes,
                        LoopCount = 0,
                        Flags = BufferFlags.None,
                    };
                    memBuffers[i].Size = frameSizeBytes;
                    memBuffers[i].Pointer = Utilities.AllocateMemory(memBuffers[i].Size);
                }

                sourceVoice = new SourceVoice(xaudio, new WaveFormat(SampleRate, BITS_PER_SAMPLE, CHANNELS), true);

                xaudio.StartEngine();

                sourceVoice.BufferEnd += (o) => bufferEndEvent?.Set();
                sourceVoice.Start();

                playingTask = Task.Factory.StartNew(Loop, TaskCreationOptions.LongRunning);

                enabled = false;
                on = false;
                mute = false;
                Stopped = false;
            }
            catch (Exception)
            {
                AnyInitFail = true;
                enabled = false;
                Stopped = true;
            }
        }

        public void Sample()
        {
            if (enabled)
            {
                // Get the z80 sound output port current value
                short outputLevel;

                outputLevel = outTable[SampleCallback() & 0x03];

                if (UseDriveNoise && DriveMotorRunning)
                    outputLevel += noise.GetNoiseSample();

                frameBuffer.Sample(outputLevel);
            }
        }

        private void UpdateEnabled()
        {
            if (enabled != (On && !Mute && !Stopped))
            {
                enabled = On && !Mute && !Stopped;
                if (enabled)
                {
                    frameBuffer.Reset();
                    noise.Reset();
                }
            }
        }
        private void Loop()
        {
            try
            {
                while (!Stopped)
                {
                    if (sourceVoice.State.BuffersQueued >= RING_SIZE)
                    {
                        bufferEndEvent.WaitOne(1);
                    }
                    else
                    {
                        var buf = memBuffers[ringCursor];

                        if (enabled)
                            frameBuffer.ReadFrame(buf);
                        else
                        {
                            frameBuffer.ReadSilentFrame(buf);
                        }
                        audioBuffersRing[ringCursor].AudioDataPointer = buf.Pointer;

                        sourceVoice.SubmitSourceBuffer(audioBuffersRing[ringCursor], null);

                        ringCursor = ++ringCursor % RING_SIZE;
                    }
                }
            }
            catch (Exception)
            {
                Mute = true;
                Dispose();
            }
        }
        public async void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                enabled = false;
                Stopped = true;

                if (playingTask != null)
                    await playingTask;

                sourceVoice?.DestroyVoice();
                sourceVoice?.Dispose();
                bufferEndEvent?.Dispose();
                masteringVoice?.Dispose();
                xaudio?.Dispose();

                sourceVoice = null;
                bufferEndEvent = null;
                masteringVoice = null;
                xaudio = null;
            }
        }
    }
}