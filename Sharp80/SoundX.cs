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
        private const int FRAME_SIZE_SAMPLES = SAMPLE_RATE / FRAMES_PER_SECOND * CHANNELS;
        private const int FRAME_SIZE_BYTES = FRAME_SIZE_SAMPLES * BYTES_PER_SAMPLE;

        private const short MAX_SOUND_AMPLITUDE = 0x40 * 0x100;
        private const short NULL_SOUND_LEVEL = 0x0;
        private const short MIN_SOUND_LEVEL = NULL_SOUND_LEVEL - MAX_SOUND_AMPLITUDE;
        private const short MAX_SOUND_LEVEL = NULL_SOUND_LEVEL + MAX_SOUND_AMPLITUDE;

        private short[] outTable = new short[] { MAX_SOUND_LEVEL, NULL_SOUND_LEVEL, MIN_SOUND_LEVEL, MIN_SOUND_LEVEL };

        private Noise noise;

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
                    if (on)
                    {
                        frameBuffer.Reset();
                        noise.Reset();
                    }
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
                    if (!mute)
                    {
                        frameBuffer.Reset();
                        noise.Reset();
                    }
                }
            }
        }
        public bool UseDriveNoise { get; set; } = false;
        public bool DriveMotorRunning { get; set; } = false;
        public bool IsDisposed { get; private set; } = false;
        public void TrackStep()
        {
            noise.TrackStep();
        }

        private const int RING_SIZE = 2;
        private int ringCursor = 0;
        private AudioBuffer[] audioBuffersRing = new AudioBuffer[RING_SIZE];
        private DataPointer[] memBuffers = new DataPointer[RING_SIZE];
        private FrameBuffer<short> frameBuffer;

        private GetSampleCallback getSampleCallback;

        public SoundX(GetSampleCallback GetSampleCallback)
        {
            xaudio = new XAudio2();
            masteringVoice = new MasteringVoice(xaudio, CHANNELS, SAMPLE_RATE);
            bufferEndEvent = new AutoResetEvent(false);

            frameBuffer = new FrameBuffer<short>(FRAME_SIZE_SAMPLES, FRAMES_PER_SECOND / 10);
            noise = new Noise(SAMPLE_RATE);

            for (int i = 0; i < RING_SIZE; i++)
            {
                audioBuffersRing[i] = new AudioBuffer()
                {
                    AudioBytes = FRAME_SIZE_BYTES,
                    LoopCount = 0,
                    Flags = BufferFlags.None,
                };
                memBuffers[i].Size = FRAME_SIZE_BYTES;
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

                if (UseDriveNoise && DriveMotorRunning)
                    outputLevel += noise.GetNoiseSample();
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
        private class Noise
        {
            private const double TRACK_STEP_WAV_1_FREQ = 783.99; // G
            private const double TRACK_STEP_WAV_2_FREQ = 523.25; // C

            private const double DRIVE_NOISE_WAV_1_FREQ = 251;
            private const double DRIVE_NOISE_WAV_2_FREQ = 133;

            private const double DRIVE_NOISE_WAV_1_PERIOD = SAMPLE_RATE / DRIVE_NOISE_WAV_1_FREQ;
            private const double DRIVE_NOISE_WAV_2_PERIOD = SAMPLE_RATE / DRIVE_NOISE_WAV_2_FREQ;

            private const double MAX_DRIVE_NOISE_AMP_1 = MAX_SOUND_AMPLITUDE / 27.0d;
            private const double MAX_DRIVE_NOISE_AMP_2 = MAX_SOUND_AMPLITUDE / 23.0d;

            private const double MAX_TRACK_NOISE_AMP = MAX_SOUND_AMPLITUDE / 30.0d;

            private const int DRIVE_NOISE_SAMPLE_SIZE = (int)(DRIVE_NOISE_WAV_1_PERIOD * DRIVE_NOISE_WAV_2_PERIOD * 2.0 * Math.PI);
            private const int TRACK_STEP_NOISE_SAMPLE_SIZE = SAMPLE_RATE / 6;

            private short[] driveNoise;
            private short[] trackStepNoise;
            private double trackWav1Period, trackWav2Period;

            Random r = new Random();

            private int noiseCursor, trackStepCursor;

            public Noise(int SampleRate)
            {
                trackWav1Period = SampleRate / TRACK_STEP_WAV_1_FREQ;
                trackWav2Period = SampleRate / TRACK_STEP_WAV_2_FREQ;

                driveNoise = new short[DRIVE_NOISE_SAMPLE_SIZE];
                trackStepNoise = new short[TRACK_STEP_NOISE_SAMPLE_SIZE];

                for (int i = 0; i < DRIVE_NOISE_SAMPLE_SIZE; ++i)
                    driveNoise[i] = (short)(MAX_DRIVE_NOISE_AMP_1 * Math.Sin(i / DRIVE_NOISE_WAV_1_PERIOD * (Math.PI * 2)) +
                                            MAX_DRIVE_NOISE_AMP_2 * Math.Sin(i / DRIVE_NOISE_WAV_2_PERIOD * (Math.PI * 2))
                                            // random noise
                                            * (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                            );

                for (int i = 0; i < TRACK_STEP_NOISE_SAMPLE_SIZE; ++i)
                {
                    trackStepNoise[i] = (short)(((MAX_TRACK_NOISE_AMP * Math.Sin(i / trackWav1Period * (Math.PI * 2)))
                                               + (MAX_TRACK_NOISE_AMP * Math.Sin(i / trackWav2Period * (Math.PI * 2)))
                                          )
                                          // Fade in/out
                                          //* (1.0 - (Math.Abs((i - TRACK_STEP_NOISE_SAMPLE_SIZE / 2.0) / (TRACK_STEP_NOISE_SAMPLE_SIZE / 2.0))))
                                          // Random noise
                                          //* (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                          );
                }
                Reset();
            }
            public void Reset()
            {
                trackStepCursor = TRACK_STEP_NOISE_SAMPLE_SIZE;
                noiseCursor = 0;
            }
            public short GetNoiseSample()
            {
                var sample = driveNoise[noiseCursor];
                noiseCursor = ++noiseCursor % DRIVE_NOISE_SAMPLE_SIZE;

                if (trackStepCursor < TRACK_STEP_NOISE_SAMPLE_SIZE)
                    sample += trackStepNoise[trackStepCursor++];

                return sample;
            }
            public void TrackStep()
            {
                trackStepCursor = 0;
            }
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

            public FrameBuffer(int FrameSize, int MinLatencyFrames)
            {
                frameSize = FrameSize;
                bufferSize = 100 * frameSize;

                minLatency = MinLatencyFrames * frameSize;
                maxLatency = minLatency * 2;
                
                buffer = new T[bufferSize];
                silentFrame = new T[frameSize];
                
                Reset();
            }
            private long resets, wrapArounds, drops, doubles, overreads, frameReads, total;
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
                else if (Latency > maxLatency)
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
                CheckOverread();
            }
            public void ReadSilentFrame(DataPointer Buffer)
            {
                Buffer.CopyFrom<T>(silentFrame, 0, frameSize);
            }
            public void ReadFrame(DataPointer Buffer)
            {
                frameReads++;

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
                CheckOverread();
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
            private void CheckOverread()
            {
                if ((writeWrap && writeCursor > readCursor) || (!writeWrap && writeCursor < readCursor + frameSize))
                {
                    overreads++;
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
