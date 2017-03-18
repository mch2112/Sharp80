/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;

using SharpDX;
using SharpDX.Multimedia;
using SharpDX.XAudio2;

namespace Sharp80
{
    internal partial class SoundX : ISound, IDisposable
    {
        public const int SAMPLE_RATE = 16000;

        private static bool AnyInitFail { get; set; } = false;
        public bool Initialized { get; private set; }
        
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
        private const int FRAME_SIZE_SAMPLES = SAMPLE_RATE / FRAMES_PER_SECOND * CHANNELS;
        private const int FRAME_SIZE_BYTES = FRAME_SIZE_SAMPLES * BYTES_PER_SAMPLE;

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

        private GetSampleCallback getSampleCallback;

        public SoundX(GetSampleCallback GetSampleCallback)
        {
            if (AnyInitFail)
            {
                Initialized = false;
                return;
            }
            try
            {
                frameBuffer = new FrameBuffer<short>(FRAME_SIZE_SAMPLES, FRAMES_PER_SECOND / 10);
                noise = new Noise(SAMPLE_RATE);
                getSampleCallback = GetSampleCallback;

                xaudio = new XAudio2();

                masteringVoice = new MasteringVoice(xaudio, CHANNELS, SAMPLE_RATE);
                
                bufferEndEvent = new AutoResetEvent(false);
                
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

                sourceVoice = new SourceVoice(xaudio, new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS), true);

                xaudio.StartEngine();
                
                sourceVoice.BufferEnd += SourceVoice_BufferEnd;
                sourceVoice.Start();
                
                playingTask = Task.Factory.StartNew(Loop, TaskCreationOptions.LongRunning);

                Initialized = enabled = true;
            }
            catch (Exception Ex)
            {
                AnyInitFail = true;
                Initialized = enabled = false;
                Log.LogException(Ex, ExceptionHandlingOptions.LogOnly);
                Dialogs.AlertUser("Failed to start XAudio2. Please update your DirectX drivers from Microsoft. Sharp 80 will continue without sound.");
            }
        }
        
        public void Sample()
        {
            if (enabled)
            {
                // Get the z80 sound output port current value
                short outputLevel;

                outputLevel = outTable[getSampleCallback() & 0x03];

                if (UseDriveNoise && DriveMotorRunning)
                    outputLevel += noise.GetNoiseSample();

                frameBuffer.Sample(outputLevel);
            }
        }
        
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                enabled = false;
                Initialized = false;
                DisposeXAudio();
            }
        }

        private void UpdateEnabled()
        {
            if (enabled != (On && !Mute && Initialized))
            {
                enabled = On && !Mute && Initialized;
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
                while (Initialized)
                {
                    if (sourceVoice.State.BuffersQueued >= RING_SIZE)
                    {
                        bufferEndEvent.WaitOne(1);
                    }
                    else
                    {
                        if (enabled)
                            frameBuffer.ReadFrame(memBuffers[ringCursor]);
                        else
                            frameBuffer.ReadSilentFrame(memBuffers[ringCursor]);

                        audioBuffersRing[ringCursor].AudioDataPointer = memBuffers[ringCursor].Pointer;

                        sourceVoice.SubmitSourceBuffer(audioBuffersRing[ringCursor], null);

                        ringCursor = ++ringCursor % RING_SIZE;
                    }
                }
            }
            catch (Exception ex)
            {
                Mute = true;
                Log.LogException(ex);
            }
        }
        private void SourceVoice_BufferEnd(IntPtr obj)
        {
            bufferEndEvent?.Set();
        }
        private void DisposeXAudio()
        {
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