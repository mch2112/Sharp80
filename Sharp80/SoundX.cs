using SharpDX;
using SharpDX.Multimedia;
using SharpDX.XAudio2;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class SoundX : IDisposable
    {
        public delegate byte GetSampleCallback();
        public delegate void SoundEventCallback();

        public const int SAMPLE_RATE = 16000;

        private XAudio2 xaudio;
        private MasteringVoice masteringVoice;
        private Task playingTask;
        private AutoResetEvent bufferEndEvent;

        private const int FRAMES_PER_SECOND = 20;
        private const int BITS_PER_SAMPLE = 16;
        private const int BYTES_PER_SAMPLE = BITS_PER_SAMPLE / 8;
        private const int CHANNELS = 1;
        private const int FRAME_SIZE = SAMPLE_RATE / FRAMES_PER_SECOND * CHANNELS * BYTES_PER_SAMPLE;

        private const short MAX_SOUND_AMPLITUDE = 0x40 * 0x100;
        private const short NULL_SOUND_LEVEL = 0x0;
        private const short MIN_SOUND_LEVEL = NULL_SOUND_LEVEL - MAX_SOUND_AMPLITUDE;
        private const short MAX_SOUND_LEVEL = NULL_SOUND_LEVEL + MAX_SOUND_AMPLITUDE;

        private short[] outTable = new short[] { MAX_SOUND_LEVEL, NULL_SOUND_LEVEL, MIN_SOUND_LEVEL, MIN_SOUND_LEVEL };

        private bool on = false;
        private bool mute = false;

        private SourceVoice sourceVoice;

        public bool On
        {
            get { return on; }
            set
            {
                if (on != value)
                {
                    on = value;
                    if (!on)
                        frameBuffer.Reset();
                }
            }
        }
        public bool Mute
        {
            get { return mute; }
            set
            {
                if (mute != value)
                {
                    mute = value;
                    if (mute)
                        frameBuffer.Reset();
                }
            }
        }
        public bool UseDriveNoise { get; set; } = false;
        public bool DriveMotorRunning { get; set; } = false;
        public bool IsDisposed { get; private set; } = false;
        public void TrackStep()
        {
            trackStepCursor = 0;
        }

        private int trackStepCursor = 0;

        private const int RING_SIZE = 3;
        private int ringCursor = 0;
        private AudioBuffer[] audioBuffersRing = new AudioBuffer[RING_SIZE];
        private DataPointer[] memBuffers = new DataPointer[RING_SIZE];
        private FrameBuffer<short> frameBuffer;

        private GetSampleCallback getSampleCallback;

        public SoundX(GetSampleCallback GetSampleCallback, IDXClient Parent)
        {
            xaudio = new XAudio2();
            masteringVoice = new MasteringVoice(xaudio, CHANNELS, SAMPLE_RATE);
            bufferEndEvent = new AutoResetEvent(false);
            
            frameBuffer = new FrameBuffer<short>(FRAME_SIZE, SAMPLE_RATE / 10);
            
            for (int i = 0; i < RING_SIZE; i++)
            {
                audioBuffersRing[i] = new AudioBuffer()
                {
                    AudioBytes = FRAME_SIZE * BYTES_PER_SAMPLE
                };
                memBuffers[i].Size = FRAME_SIZE * BYTES_PER_SAMPLE;
                memBuffers[i].Pointer = Utilities.AllocateMemory(memBuffers[i].Size);
            }
            getSampleCallback = GetSampleCallback;
            
            sourceVoice = new SourceVoice(xaudio, new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS), true);
            
            xaudio.StartEngine();

            sourceVoice.BufferEnd += SourceVoice_BufferEnd;
            sourceVoice.Start();
            
            playingTask = Task.Factory.StartNew(Loop, TaskCreationOptions.LongRunning);
        }
        private void Loop()
        {
            while (!IsDisposed)
            {
                if (sourceVoice.State.BuffersQueued >= RING_SIZE)
                {
                    bufferEndEvent.WaitOne(1);
                }
                else
                {
                    if (On && !Mute)
                        frameBuffer.ReadFrame(memBuffers[ringCursor]);
                    else
                        frameBuffer.ReadSilentFrame(memBuffers[ringCursor]);

                    audioBuffersRing[ringCursor].AudioDataPointer = memBuffers[ringCursor].Pointer;

                    sourceVoice.SubmitSourceBuffer(audioBuffersRing[ringCursor], null);

                    ringCursor = ++ringCursor % RING_SIZE;
                }
            }
        }
        private Random r = new Random();
        public void Sample()
        {
            if (On && !Mute)
            {
                // Get the z80 sound output port current value
                short outputLevel;

                outputLevel = outTable[getSampleCallback() & 0x03];

                //outputLevel = (short)r.Next(-0x4000, 0x4000);

                frameBuffer.Sample(outputLevel);
            }
        }
        
        public void Dispose()
        {
            IsDisposed = true;

            sourceVoice.DestroyVoice();
            sourceVoice.Dispose();
            masteringVoice.Dispose();
            xaudio.Dispose();
        }
        private void SourceVoice_BufferEnd(IntPtr obj)
        {
            bufferEndEvent.Set();
        }
        private class FrameBuffer<T> where T:struct
        {
            private int bufferSize;
            private int readCursor;
            private int writeCursor;
            private int frameSize;
            private int minLatency;
            private int maxLatency;

            private bool writeWrap;

            private T[] buffer;
            private T[] silentFrame;

            public FrameBuffer(int FrameSize, int MinLatency)
            {
                frameSize = FrameSize;
                bufferSize = 100 * frameSize;

                minLatency = MinLatency;
                maxLatency = minLatency * 2;
                
                buffer = new T[bufferSize];
                silentFrame = new T[frameSize];
                
                Reset();
            }
            private long resets, wrapArounds, drops, doubles, total;
            public void Sample(T Val)
            {
                total++;
                if (Latency < minLatency)
                {
                    doubles++;
                    // double sample
                    buffer[writeCursor++] = Val;
                    ZeroWriteCursor();
                    buffer[writeCursor++] = Val;
                    ZeroWriteCursor();
                }
                else if (Latency > minLatency * 2)
                {
                    drops++;
                    // drop sample
                }
                else
                {
                    // Normal 
                    buffer[writeCursor++] = Val;
                    ZeroWriteCursor();
                }
                CheckOk();
            }
            public void ReadSilentFrame(DataPointer Buffer)
            {
                Buffer.CopyFrom<T>(silentFrame, 0, frameSize);
            }
            public void ReadFrame(DataPointer Buffer)
            {
                int startFrame = readCursor;
                Buffer.CopyFrom<T>(buffer, readCursor, frameSize);

                readCursor += frameSize;

                if (readCursor >= bufferSize)
                {
                    readCursor = 0;
                    if (writeWrap)
                        writeWrap = false;
                    else
                        WrapAround();
                }
                CheckOk();
            }
            public void Reset()
            {
                readCursor = 0;
                writeCursor = maxLatency * 2;
                Array.Clear(buffer, 0, writeCursor);
                writeWrap = false;
                resets++;
            }
            private int Latency
            {
                get { return (writeCursor + bufferSize - readCursor) % bufferSize; }
            }
            private void ZeroWriteCursor()
            {
                if (writeCursor >= bufferSize)
                {
                    writeCursor = 0;

                    if (writeWrap)
                        WrapAround();
                    else
                        writeWrap = true;
                }
            }
            private void CheckOk()
            {
                if ((writeWrap && writeCursor > readCursor) || (!writeWrap && writeCursor < readCursor + frameSize))
                {
                    //Log.LogMessage(string.Format("Sound Buffer Error: Read: {0} Write: {1} Frame Size: {2} Wrap: {3}", readCursor, writeCursor, frameSize, writeWrap ? "Yes" : "No"));
                    Reset();
                }
            }
            private void WrapAround()
            {
                wrapArounds++;
                Reset();
            }            
        }
    }
}
