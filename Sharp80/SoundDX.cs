using System;
using System.Threading;

using SharpDX;
using SharpDX.DirectSound;

namespace Sharp80
{
    internal sealed class __SoundDX : IDisposable
    {
        private readonly Object LOCK_THREAD_INSTANCE = new object();
        private readonly Object LOCK = new object();

        // DELEGATES

        public delegate void SoundEventCallback();
        public delegate byte GetSampleCallback();
        private SoundEventCallback onCallback;
        private SoundEventCallback offCallback;
        private GetSampleCallback getSampleCallback;

        // CONSTANTS

        public const int SAMPLE_RATE = 0x4000;

        private const int BITS_PER_SAMPLE = 16;
        private const int NUM_CHANNELS = 1;

        private const int FRAMES_PER_DX_BUFFER = 2;
        private const int FRAME_SIZE = SAMPLE_RATE / 0x20;
        private const int FRAME_SIZE_IN_BYTES = FRAME_SIZE * BITS_PER_SAMPLE / 8;
        
        private const double TRACK_STEP_WAV_1_FREQ = 783.99; // G
        private const double TRACK_STEP_WAV_2_FREQ = 523.25; // C

        private const double TRACK_STEP_WAV_1_PERIOD = SAMPLE_RATE / TRACK_STEP_WAV_1_FREQ;
        private const double TRACK_STEP_WAV_2_PERIOD = SAMPLE_RATE / TRACK_STEP_WAV_2_FREQ;

        private const double DRIVE_NOISE_WAV_1_FREQ = 251;
        private const double DRIVE_NOISE_WAV_2_FREQ = 133;

        private const double DRIVE_NOISE_WAV_1_PERIOD = SAMPLE_RATE / DRIVE_NOISE_WAV_1_FREQ;
        private const double DRIVE_NOISE_WAV_2_PERIOD = SAMPLE_RATE / DRIVE_NOISE_WAV_2_FREQ;
        
        private const short MAX_SOUND_AMPLITUDE = 0x40 * 0x100;
        private const short NULL_SOUND_LEVEL = 0x0;
        private const short MIN_SOUND_LEVEL = NULL_SOUND_LEVEL - MAX_SOUND_AMPLITUDE;
        private const short MAX_SOUND_LEVEL = NULL_SOUND_LEVEL + MAX_SOUND_AMPLITUDE;

        private const double MAX_DRIVE_NOISE_AMP_1 = MAX_SOUND_AMPLITUDE / 27.0d;
        private const double MAX_DRIVE_NOISE_AMP_2 = MAX_SOUND_AMPLITUDE / 23.0d;

        private const double MAX_TRACK_NOISE_AMP = MAX_SOUND_AMPLITUDE / 30.0d;

        private const int DRIVE_NOISE_SAMPLE_SIZE = (int)(DRIVE_NOISE_WAV_1_PERIOD * DRIVE_NOISE_WAV_2_PERIOD * 2.0 * Math.PI);
        private const int TRACK_STEP_NOISE_SAMPLE_SIZE = SAMPLE_RATE / 6;

        // STATE

        private Thread refreshThread;
        private AutoResetEvent[] notificationEvents;
        private DirectSound directSound;
        private PrimarySoundBuffer primaryBuffer;
        private SecondarySoundBuffer secondaryBuffer;

        private short[] soundCaptureBuffer = new short[FRAME_SIZE];
        private short[] silentSoundBuffer = new short[FRAME_SIZE];
        private short[] driveNoise = new short[DRIVE_NOISE_SAMPLE_SIZE];
        private short[] trackStep = new short[TRACK_STEP_NOISE_SAMPLE_SIZE];
        private short[] outTable;

        private int frameCursor;
        private int trackStepCursor;
        private int driveNoiseCursor;

        private bool isDisposed = false;
        private bool disposing = false;

        private Random r = new Random();

        // CONSTRUCTOR
        public __SoundDX(GetSampleCallback GetSampleCallback, IDXClient Parent, bool On)
        {
            this.On = On;

            InitializeSoundCaptureBuffers();

            getSampleCallback = GetSampleCallback;

            directSound = new DirectSound();
            directSound.SetCooperativeLevel(Parent.Handle, CooperativeLevel.Priority);

            // Setup Primary BUffer
            var primaryBufferDesc = new SoundBufferDescription()
            {
                Flags = BufferFlags.PrimaryBuffer,
                AlgorithmFor3D = Guid.Empty
            };
            primaryBuffer = new PrimarySoundBuffer(directSound, primaryBufferDesc);
            primaryBuffer.Play(0, PlayFlags.Looping);

            // Setup Secondary Buffer
            var sbd = new SoundBufferDescription()
            {
                BufferBytes = FRAME_SIZE_IN_BYTES * FRAMES_PER_DX_BUFFER,
                Format = new SharpDX.Multimedia.WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, NUM_CHANNELS),
                Flags = BufferFlags.GetCurrentPosition2 | BufferFlags.ControlPositionNotify | BufferFlags.StickyFocus,
                AlgorithmFor3D = Guid.Empty
            };
            secondaryBuffer = new SecondarySoundBuffer(directSound, sbd);

            // Setup notification triggers, beginning of buffer and 1/2 way through
            notificationEvents = new AutoResetEvent[FRAMES_PER_DX_BUFFER];
            NotificationPosition[] np = new NotificationPosition[FRAMES_PER_DX_BUFFER];
            for (int i = 0; i < FRAMES_PER_DX_BUFFER; i++)
            {
                np[i] = new NotificationPosition()
                {
                    Offset = i * FRAME_SIZE_IN_BYTES,
                    WaitHandle = notificationEvents[i] = new AutoResetEvent(false)
                };
            }
            secondaryBuffer.SetNotificationPositions(np);

            // Start thread to feed secondary buffer
            ThreadStart threadDelegate = delegate()
            {
                try { FeedBuffer(); }
                catch (ThreadAbortException) { /* Do Nothing */ }
                finally { lock (LOCK_THREAD_INSTANCE) { refreshThread = null; } }
            };

            lock (LOCK_THREAD_INSTANCE)
            {
                refreshThread = new Thread(threadDelegate)
                {
                    Priority = ThreadPriority.AboveNormal,
                    IsBackground = true
                };
                refreshThread.Start();
            }
            // Start the secondary buffer
            secondaryBuffer.Play(0, PlayFlags.Looping);
        }

        // PUBLIC INTERFACE
        public bool On { get; set; }
        public bool Mute { get; set; }
        public bool UseDriveNoise { get; set; }
        public bool DriveMotorRunning { get; set; }
        public void TrackStep()
        {
            trackStepCursor = 0;
        }
        public void Sample()
        {
            lock (LOCK)
            {
                System.Diagnostics.Debug.Assert(frameCursor <= soundCaptureBuffer.Length);

                // Get the z80 sound output port current value
                short outputLevel;

                outputLevel = outTable[getSampleCallback() & 0x03];

                if (UseDriveNoise)
                {
                    // Plus drive noise if running
                    if (DriveMotorRunning)
                    {
                        if (driveNoiseCursor >= DRIVE_NOISE_SAMPLE_SIZE)
                            driveNoiseCursor = 0;
                        outputLevel += driveNoise[driveNoiseCursor++];
                    }
                    // And track stepping noise
                    if (trackStepCursor < TRACK_STEP_NOISE_SAMPLE_SIZE)
                        outputLevel += trackStep[trackStepCursor++];
                }

                // Save in buffer
                if (frameCursor < FRAME_SIZE)
                    soundCaptureBuffer[frameCursor++] = outputLevel;
                else
                    fails++;
                tries++;

                // And suspend z80 if buffer full
                //if (frameCursor >= FRAME_SIZE)
                  //  offCallback();
            }
        }
        private long tries = 0;
        private long fails = 0;
        public SoundEventCallback ThrottleOnCallback
        {
            set { lock (LOCK) { onCallback = value; } }
        }
        public SoundEventCallback ThrottleOffCallback
        {
            set { lock (LOCK) { offCallback = value; } }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                lock (LOCK)
                {
                    disposing = true;

                    onCallback?.Invoke();

                    Thread.Sleep(10);

                    secondaryBuffer.Stop();

                    if (!secondaryBuffer.IsDisposed)
                        secondaryBuffer.Dispose();

                    primaryBuffer.Stop();
                    if (!primaryBuffer.IsDisposed)
                        primaryBuffer.Dispose();

                    if (!directSound.IsDisposed)
                        directSound.Dispose();
                }
                isDisposed = true;
            }
        }
        public bool IsDisposed { get { return isDisposed; } }

        // INTERNAL METHODS
        private void InitializeSoundCaptureBuffers()
        {
            System.Diagnostics.Debug.Assert((MAX_SOUND_AMPLITUDE +
                                             MAX_DRIVE_NOISE_AMP_1 +
                                             MAX_DRIVE_NOISE_AMP_2 +
                                             MAX_TRACK_NOISE_AMP) < 0x7FFF);

            for (int i = 0; i < FRAME_SIZE; i++)
                soundCaptureBuffer[i] = silentSoundBuffer[i] = NULL_SOUND_LEVEL;

            outTable = new short[] { MAX_SOUND_LEVEL,
                                     NULL_SOUND_LEVEL,
                                     MIN_SOUND_LEVEL,
                                     MIN_SOUND_LEVEL };

            for (int i = 0; i < DRIVE_NOISE_SAMPLE_SIZE; ++i)
                driveNoise[i] = (short)(MAX_DRIVE_NOISE_AMP_1 * Math.Sin(i / DRIVE_NOISE_WAV_1_PERIOD * (Math.PI * 2)) +
                                        MAX_DRIVE_NOISE_AMP_2 * Math.Sin(i / DRIVE_NOISE_WAV_2_PERIOD * (Math.PI * 2))
                                        // random noise
                                        * (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                        );

            for (int i = 0; i < TRACK_STEP_NOISE_SAMPLE_SIZE; ++i)
            {
                trackStep[i] = (short)(((MAX_TRACK_NOISE_AMP * Math.Sin(i / TRACK_STEP_WAV_1_PERIOD * (Math.PI * 2)))
                                      + (MAX_TRACK_NOISE_AMP * Math.Sin(i / TRACK_STEP_WAV_2_PERIOD * (Math.PI * 2)))
                                      )
                                      // Fade in/out
                                      * (1.0 - (Math.Abs((i - TRACK_STEP_NOISE_SAMPLE_SIZE / 2.0) / (TRACK_STEP_NOISE_SAMPLE_SIZE / 2.0))))
                                      // Random noise
                                      //* (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                      );
            }
            trackStepCursor = TRACK_STEP_NOISE_SAMPLE_SIZE;
            driveNoiseCursor = 0;
            frameCursor = 0;
        }
        private void FeedBuffer()
        {
            while (!disposing)
            {
                //lock (LOCK) { onCallback?.Invoke(); }

                int eventIndex = WaitHandle.WaitAny(notificationEvents);

                if (!disposing)
                {
                    lock (LOCK)
                    {
                        try
                        {
                            short[] buffer = (On && !Mute) ? soundCaptureBuffer : silentSoundBuffer;

                            DataStream dataPart1 = secondaryBuffer.Lock(eventIndex * FRAME_SIZE_IN_BYTES,
                                                                        FRAME_SIZE_IN_BYTES,
                                                                        LockFlags.EntireBuffer,
                                                                        out DataStream dataPart2);

                            dataPart1.WriteRange(buffer);
                            secondaryBuffer.Unlock(dataPart1, dataPart2);

                            frameCursor = 0;
                        }
                        catch (Exception ex)
                        {
                            Log.LogMessage(ex.ToString());
                            break;
                        }
                    }
                }
            }
        }
    }
}