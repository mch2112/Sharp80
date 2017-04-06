/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    public enum ClockSpeed { XXSlow = 0, XSlow = 1, Slow = 2, Normal = 3, Fast = 4, Unlimited = 5, Count = 6 }

    public class Clock : ISerializable
    {
        public event EventHandler SpeedChanged;

        public const ulong CLOCK_RATE = 2027520;

        internal const ushort TICKS_PER_TSTATE = 1000;
        internal const ulong TICKS_PER_SECOND = CLOCK_RATE * TICKS_PER_TSTATE;

        /// <summary>
        /// This is near 30 hz but not exactly since we don't want to sync with Floppy disk
        /// angle. If sync'd, IRQs occur at exactly six disk angles rather than being mostly
        /// random. LDOS which has interupts enabled when reading sectors which when causes
        /// certain sectors to become unreadable, because an interrupt always dirupts reading
        /// those sectors.
        /// </summary>
        internal const ulong TICKS_PER_IRQ = TICKS_PER_SECOND * 100 / 3001;

        // Internal State

        private ITimer timer;
        private long realTimeElapsedTicksOffset;
        private double[] z80TicksPerRealtimeTick;
        private ulong[] speedMeasureTickInterval;
        private string[] clockReportFormat;

        private ulong emuSpeedInHz;
        private ulong z80TicksOnLastMeasure;
        private ulong nextEmuSpeedMeasureTicks;
        private long realTimeTicksOnLastMeasure;
        private ulong waitTimeout;

        private ClockSpeed clockSpeed;
        private bool stopReq = false;

        private Task execThread;

        private Computer computer;
        private Z80.Z80 z80;
        private InterruptManager IntMgr;

        private PulseScheduler pulseScheduler = new PulseScheduler();
        private ulong nextRtcIrqTick;

        private Action soundCallback;
        private ulong nextSoundSampleTick;
        private readonly ulong ticksPerSoundSample;

        private Trigger waitTrigger;

        // CONSTRUCTOR

        internal Clock(Computer Computer, Z80.Z80 Processor, ITimer Timer, InterruptManager InterruptManager, ulong TicksPerSoundSample, Action SoundCallback)
        {
            InitTickMeasurement(Timer.TicksPerSecond);

            TickCount = 0;

            computer = Computer;
            z80 = Processor;
            timer = Timer;
            IntMgr = InterruptManager;

            ticksPerSoundSample = TicksPerSoundSample;
            soundCallback = SoundCallback;
            clockSpeed = ClockSpeed.Normal;

            nextRtcIrqTick = TICKS_PER_IRQ;

            ResetTriggers();

            waitTrigger = new Trigger(null,
                                      null,
                                      TriggerLock: false,
                                      CanLatchBeforeEnabled: false)
            { Enabled = true };
        }

        // PROPERTIES

        public bool IsRunning { get; private set; }
        public bool IsStarting { get; private set; }
        public bool IsStopped => !IsRunning && !IsStarting;
        public ulong ElapsedTStates => TickCount / TICKS_PER_TSTATE;
        internal ulong TickCount { get; private set; }
        internal string GetInternalsReport()
        {
            var s = new StringBuilder();

            if (!IsRunning)
            {
                emuSpeedInHz = 0;
            }
            else
            {
                if (TickCount > nextEmuSpeedMeasureTicks)
                {
                    ulong z80TickDiff = TickCount - z80TicksOnLastMeasure;
                    long currentRealTimeTicks = timer.ElapsedTicks;
                    long realTimeTickDiff = currentRealTimeTicks - realTimeTicksOnLastMeasure;

                    emuSpeedInHz = (ulong)(timer.TicksPerSecond * (double)z80TickDiff / realTimeTickDiff) / TICKS_PER_TSTATE;

                    nextEmuSpeedMeasureTicks = TickCount + speedMeasureTickInterval[(int)ClockSpeed];

                    z80TicksOnLastMeasure = TickCount;
                    realTimeTicksOnLastMeasure = currentRealTimeTicks;
                }
            }
            s.Append(((float)emuSpeedInHz / 1000000f).ToString(clockReportFormat[(int)ClockSpeed]));

            s.Append(" T: " + (TickCount / TICKS_PER_TSTATE).ToString("000,000,000,000,000"));

            if (waitTrigger.Latched)
                s.Append(" WAIT");

            return s.ToString();
        }

        // CPU CONTROL

        internal void Start()
        {
            if (IsStopped)
            {
                IsStarting = true;
                ResetTriggers();
                execThread = Task.Run(Exec);
            }
        }
        internal void Stop() => stopReq = true;
        internal async Task StopAndAwait()
        {
            Stop();
            if (execThread != null)
                await execThread;
        }
        internal void Step()
        {
            if (IsStopped)
                ExecOne();
        }
        internal void Wait()
        {
            if (!waitTrigger.Latched)
                waitTimeout = TickCount + (TICKS_PER_SECOND * 1024ul / 1000000ul); // max 1024 usec
            waitTrigger.Latch();
        }
        internal ClockSpeed ClockSpeed
        {
            get => clockSpeed;
            set
            {
                if (clockSpeed != value)
                {
                    ResetTriggers();
                    clockSpeed = value;
                    SpeedChanged?.Invoke(this, EventArgs.Empty);
                    SyncRealTimeOffset();
                }
            }
        }

        // CPU THREAD

        /// <summary>
        /// The main CPU exec loop
        /// </summary>
        private async Task Exec()
        {
            if (!IsRunning)
            {
                System.Diagnostics.Debug.Assert(IsStarting);
                IsRunning = true;
                IsStarting = false;
                stopReq = false;
                SyncRealTimeOffset();

                while (!stopReq)
                {
                    ExecOne();
                    if (clockSpeed != ClockSpeed.Unlimited)
                        await Throttle();
                }
                IsRunning = false;
            }
        }

        /// <summary>
        /// Execute a single instruction
        /// </summary>
        private void ExecOne()
        {
            // Check triggers

            if (TickCount > nextRtcIrqTick)
            {
                while (TickCount > nextRtcIrqTick)
                    nextRtcIrqTick += TICKS_PER_IRQ;
                IntMgr.RtcIntLatch.Latch();
            }

            if (waitTrigger.Latched)
            {
                if (TickCount >= waitTimeout ||
                    computer.FloppyControllerDrq ||
                    IntMgr.FdcNmiLatch.Latched ||
                    IntMgr.ResetButtonLatch.Latched)
                {
                    waitTrigger.Unlatch();
                    waitTrigger.ResetTrigger();
                }
            }

            // Execute something

            if (IntMgr.Nmi && z80.CanNmi)
            {
                IntMgr.ResetNmiTriggers();

                z80.NonMaskableInterrupt();
                TickCount += (11 * TICKS_PER_TSTATE);
            }
            else if (waitTrigger.Latched)
            {
                TickCount += TICKS_PER_TSTATE;
            }
            else if (IntMgr.Irq && z80.CanInterrupt)
            {
                IntMgr.RtcIntLatch.ResetTrigger();
                TickCount += z80.Interrupt();
            }
            else
            {
                TickCount += z80.Exec();
            }

            // Do callbacks
            pulseScheduler.Execute(TickCount);            
        }
        private async Task Throttle()
        {
            if (TickCount > nextSoundSampleTick)
            {
                soundCallback();
                nextSoundSampleTick += ticksPerSoundSample;
            }
            // at z80 speed, how many ticks should have elapsed by now?
            ulong virtualTicksReal = (ulong)((timer.ElapsedTicks - realTimeElapsedTicksOffset) * z80TicksPerRealtimeTick[(int)ClockSpeed]);

            // Are we ahead? Slow down if so
            if (TickCount > virtualTicksReal)
            {
                // How far ahead are we?
                var usecAhead = (TickCount - virtualTicksReal) * 1000000 / TICKS_PER_SECOND;

                // Sleep
                if (usecAhead < 1000)
                    await Task.Yield();
                else if (usecAhead < 100000)
                    await Task.Delay((int)usecAhead / 1000);
                else
                    await Task.Delay(100);
            }
        }

        // PULSE REQ CALLBACKS

        internal void RegisterPulseReq(PulseReq Req, bool SetTrigger) => pulseScheduler.RegisterPulseReq(Req, SetTrigger, TickCount);

        /// <summary>
        /// This zeroes the difference between the virtual and realtime clocks
        /// in case of large differences (like when starting or changing speeds)
        /// </summary>
        private void SyncRealTimeOffset()
        {
            realTimeElapsedTicksOffset = timer.ElapsedTicks - (long)(TickCount / z80TicksPerRealtimeTick[(int)ClockSpeed]);
        }
        private void ResetTriggers()
        {
            nextEmuSpeedMeasureTicks = TickCount;
            nextSoundSampleTick = TickCount;

            z80TicksOnLastMeasure = TickCount;
            realTimeElapsedTicksOffset = realTimeTicksOnLastMeasure = timer.ElapsedTicks;
            emuSpeedInHz = 0;
        }
        
        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(TickCount);
            Writer.Write(nextRtcIrqTick);
            Writer.Write(stopReq);
            Writer.Write(ulong.MaxValue);
            Writer.Write(nextSoundSampleTick);
            Writer.Write(waitTimeout);

            waitTrigger.Serialize(Writer);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                TickCount = Reader.ReadUInt64();
                nextRtcIrqTick = Reader.ReadUInt64();
                stopReq = Reader.ReadBoolean();
                Reader.ReadUInt64(); // unused
                nextSoundSampleTick = Reader.ReadUInt64();
                waitTimeout = Reader.ReadUInt64();

                return waitTrigger.Deserialize(Reader, DeserializationVersion);
            }
            catch
            {
                return false;
            }
        }

        // SETUP

        private void InitTickMeasurement(double TicksPerSecond)
        {
            var z80TicksPerRtTick = TICKS_PER_SECOND / TicksPerSecond;

            z80TicksPerRealtimeTick = new double[]
            {
                z80TicksPerRtTick / 2000,
                z80TicksPerRtTick / 200,
                z80TicksPerRtTick / 20,
                z80TicksPerRtTick,
                z80TicksPerRtTick * 2,
                z80TicksPerRtTick * 1000000
            };

            speedMeasureTickInterval = new ulong[] { 500000, 5000000, 50000000, 1000000000, 2000000000, 40000000000 };

            clockReportFormat = new string[]
            {
                "#0.0000 MHz",
                "#0.000 MHz",
                "#0.000 MHz",
                "#0.00 MHz",
                "#0.00 MHz",
                "#0.00 MHz"
            };
        }
    }
}
