/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Sharp80.Z80;

namespace Sharp80.TRS80
{
    public class Clock : ISerializable
    {
        public event EventHandler SpeedChanged;
        public delegate void ClockCallback();

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
        private double z80TicksPerRealtimeTick;

        private ulong emuSpeedInHz;
        private ulong z80TicksOnLastMeasure;
        private ulong nextEmuSpeedMeasureTicks;
        private long realTimeTicksOnLastMeasure;
        private ulong waitTimeout;

        private bool normalSpeed;
        private bool stopReq = false;

        private Task execThread;

        private Computer computer;
        private Z80.Z80 z80;
        private InterruptManager IntMgr;

        private List<PulseReq> pulseReqs = new List<PulseReq>();
        private ulong nextPulseReqTick = UInt64.MaxValue;
        private ulong nextRtcIrqTick;

        private SoundEventCallback soundCallback;
        private ulong nextSoundSampleTick;
        private readonly ulong ticksPerSoundSample;

        private Trigger waitTrigger;

        private long cyclesUntilNextSyncCheck = 0;

        // CONSTRUCTOR

        internal Clock(Computer Computer, Z80.Z80 Processor, ITimer Timer, InterruptManager InterruptManager, ulong TicksPerSoundSample, SoundEventCallback SoundCallback)
        {
            z80TicksPerRealtimeTick = TICKS_PER_SECOND / Timer.TicksPerSecond;

            TickCount = 0;

            computer = Computer;
            z80 = Processor;
            timer = Timer;
            IntMgr = InterruptManager;

            ticksPerSoundSample = TicksPerSoundSample;
            soundCallback = SoundCallback;
            normalSpeed = true;
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
                    double realTimeTickDiff = currentRealTimeTicks - realTimeTicksOnLastMeasure;

                    emuSpeedInHz = (ulong)(timer.TicksPerSecond * (double)z80TickDiff / realTimeTickDiff) / TICKS_PER_TSTATE;

                    nextEmuSpeedMeasureTicks = TickCount + emuSpeedInHz * 500; // 1/2 sec interval

                    z80TicksOnLastMeasure = TickCount;
                    realTimeTicksOnLastMeasure = currentRealTimeTicks;
                }
            }
            s.Append(((float)emuSpeedInHz / 1000000f).ToString("#0.00") + " MHz ");

            s.Append("T: " + (TickCount / TICKS_PER_TSTATE).ToString("000,000,000,000,000"));

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
        internal bool NormalSpeed
        {
            get => normalSpeed;
            set
            {
                if (normalSpeed != value)
                {
                    ResetTriggers();
                    normalSpeed = value;
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
                    if (NormalSpeed)
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
            if (TickCount > nextPulseReqTick)
            {
                // descending to avoid problems with new reqs being added
                for (int i = pulseReqs.Count - 1; i >= 0; i--)
                {
                    if (TickCount > pulseReqs[i].Trigger)
                        pulseReqs[i].Execute();
                }
                pulseReqs.RemoveAll(r => r.Inactive);
                SetNextPulseReqTick();
            }
        }
        private async Task Throttle()
        {
            System.Diagnostics.Debug.Assert(NormalSpeed);

            if (TickCount > nextSoundSampleTick)
            {
                soundCallback();
                nextSoundSampleTick += ticksPerSoundSample;
            }
            if (--cyclesUntilNextSyncCheck <= 0)
            {
                SyncRealTimeOffset();
                cyclesUntilNextSyncCheck = 200000;   // couple times a second
            }
            else
            {
                // at z80 speed, how many ticks should have elapsed by now?
                ulong virtualTicksReal = (ulong)((timer.ElapsedTicks - realTimeElapsedTicksOffset) * z80TicksPerRealtimeTick);

                // Are we ahead? Slow down if so
                if (TickCount > virtualTicksReal)
                {
                    // How far ahead are we?
                    ulong virtualTicksAhead = TickCount - virtualTicksReal;

                    // Sleep
                    if (virtualTicksAhead > 0)
                        if (virtualTicksAhead > TICKS_PER_SECOND / 1000) // 1 msec
                            await Task.Delay(1);
                        else
                            await Task.Yield();
                }
            }
        }

        // PULSE REQ CALLBACKS

        internal void ActivatePulseReq(PulseReq Req)
        {
            Req.SetTrigger(BaselineTicks: TickCount);
            AddPulseReq(Req);
        }
        internal void AddPulseReq(PulseReq Req)
        {
            if (!pulseReqs.Contains(Req))
                pulseReqs.Add(Req);

            SetNextPulseReqTick();
        }
 
        /// <summary>
        /// This zeroes the difference between the virtual and realtime clocks
        /// in case of large differences (like when starting or changing speeds)
        /// </summary>
        private void SyncRealTimeOffset()
        {
            long z80EquivalentTicks = (long)(TickCount / z80TicksPerRealtimeTick);
            long newRealTimeElapsedTicksOffset = timer.ElapsedTicks - z80EquivalentTicks;

            // if we're already synced to within 100 microseconds don't bother
            if (Math.Abs(newRealTimeElapsedTicksOffset - realTimeElapsedTicksOffset) > 100000)
                realTimeElapsedTicksOffset = newRealTimeElapsedTicksOffset;
        }
        private void SetNextPulseReqTick()
        {
            if (pulseReqs.Count == 0)
                nextPulseReqTick = UInt64.MaxValue; // no need to check until a pulseReq is added
            else
                nextPulseReqTick = 0; // check asap
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
            Writer.Write(nextPulseReqTick);
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
                nextPulseReqTick = Reader.ReadUInt64();
                nextSoundSampleTick = Reader.ReadUInt64();
                waitTimeout = Reader.ReadUInt64();

                return waitTrigger.Deserialize(Reader, DeserializationVersion);
            }
            catch
            {
                return false;
            }
        }
    }
}
