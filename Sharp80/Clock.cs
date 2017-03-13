/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp80
{
    internal sealed class Clock : ISerializable
    {
        public event EventHandler SpeedChanged;
        public delegate void ClockCallback();
        private delegate void VoidDelegate();

        public const ushort TICKS_PER_TSTATE = 1000;
        private const int MSEC_PER_SLEEP = 1;

        // Internal State

        private readonly ulong ticksPerSec;
        private readonly ulong ticksPerIRQ;

        private ulong tickCount;
        private ulong emuSpeedInHz;
        private ulong z80TicksOnLastMeasure;
        private ulong nextEmuSpeedMeasureTicks;
        private ulong nextRtcIrqTick;
        private long realTimeTicksOnLastMeasure;
        private ulong waitTimeout;

        private bool normalSpeed;
        private bool exitExec = false;

        private Computer computer;
        private Processor.Z80 z80;
        private InterruptManager IntMgr;

        private List<PulseReq> pulseReqs = new List<PulseReq>();
        private ulong nextPulseReqTick = UInt64.MaxValue;

        private SoundEventCallback soundCallback;
        private ulong nextSoundSampleTick;
        private readonly ulong ticksPerSoundSample;

        private Trigger waitTrigger;

        private long cyclesUntilNextSyncCheck = 0;
        private long cyclesUntilNextThrottleSleep = 0;

        // CONSTRUCTOR

        public Clock(Computer Computer, Processor.Z80 Processor, InterruptManager InterruptManager, ulong FrequencyInHz, ulong TicksPerIrq, ulong TicksPerSoundSample, SoundEventCallback SoundCallback, bool NormalSpeed)
        {
            ticksPerSec = FrequencyInHz * TICKS_PER_TSTATE;
            ticksPerIRQ = TicksPerIrq;

            CalRealTimeClock();

            tickCount = 0;

            computer = Computer;
            z80 = Processor;
            IntMgr = InterruptManager;
            
            ticksPerSoundSample = TicksPerSoundSample;            
            soundCallback = SoundCallback;
            normalSpeed = NormalSpeed;
            nextRtcIrqTick = ticksPerIRQ;

            PulseReq.SetTicksPerSec(ticksPerSec);

            ResetTriggers();

            waitTrigger = new Trigger(() => { Log.LogDebug(string.Format("CPU Wait ON")); },
                                      () => { Log.LogDebug(string.Format("CPU Wait OFF")); },
                                      TriggerLock: false,
                                      CanLatchBeforeEnabled: false)
                         { Enabled = true };
        }

        // PROPERTIES

        public ulong ElapsedTStates
        {
            get { return tickCount / TICKS_PER_TSTATE; }
        }
        public ulong TicksPerSec
        {
            get { return ticksPerSec; }
        }
        public ulong NextRtcIrqTick
        {
            get { return nextRtcIrqTick; }
            private set { nextRtcIrqTick = value; }
        }
        public ulong TickCount
        {
            get { return tickCount; }
            private set { tickCount = value; }
        }

        public bool NormalSpeed
        {
            get { return normalSpeed; }
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
        public void Start()
        {
            if (!IsRunning)
            {
                ResetTriggers();
                LaunchOnSeparateThread(Exec);
                SyncRealTimeOffset();
            }
        }
        public void Stop()
        {
            if (IsRunning)
                exitExec = true;
        }
        public bool IsRunning { get; private set; }

        // PUBLIC METHODS

        public void Step()
        {
            if (!IsRunning)
                ExecOne();
        }
        public void Wait()
        {
            if (!waitTrigger.Latched)
                waitTimeout = tickCount + (ticksPerSec * 1024ul / 1000000ul); // max 1024 usec
            
            waitTrigger.Latch();
        }
        public void RegisterPulseReq(PulseReq Req)
        {
            Req.SetTrigger(BaselineTicks: tickCount);

            AddPulseReq(Req);
        }
        public void AddPulseReq(PulseReq Req)
        {
            if (!pulseReqs.Contains(Req))
                pulseReqs.Add(Req);

            SetNextPulseReqTick();
        }

        public string GetInternalsReport()
        {
            var s = new StringBuilder();

            if (!IsRunning)
            {
                emuSpeedInHz = 0;
            }
            else
            {
                if (tickCount > nextEmuSpeedMeasureTicks)
                {
                    ulong z80TickDiff = tickCount - z80TicksOnLastMeasure;
                    long currentRealTimeTicks = RealTimeTicks;
                    double realTimeTickDiff = currentRealTimeTicks - realTimeTicksOnLastMeasure;

                    emuSpeedInHz = (ulong)(realTimeTicksPerSec * (double)z80TickDiff / realTimeTickDiff) / TICKS_PER_TSTATE;

                    nextEmuSpeedMeasureTicks = tickCount + emuSpeedInHz * 500; // 1/2 sec interval

                    z80TicksOnLastMeasure = tickCount;
                    realTimeTicksOnLastMeasure = currentRealTimeTicks;
                }
            }
            s.Append(((float)emuSpeedInHz / 1000000f).ToString("#0.00") + " MHz ");

            s.Append("T: " + (tickCount / TICKS_PER_TSTATE).ToString("000,000,000,000,000"));

            if (waitTrigger.Latched)
                s.Append(" WAIT");

            return s.ToString();
        }
        
        private void ResetTriggers()
        {
            nextEmuSpeedMeasureTicks = tickCount;
            nextSoundSampleTick = tickCount;
            z80TicksOnLastMeasure = tickCount;
            realTimeElapsedTicksOffset = realTimeTicksOnLastMeasure = RealTimeTicks;
            
            emuSpeedInHz = 0;
        }
        private void Exec()
        {
            if (!IsRunning)
            {
                try
                {
                    IsRunning = true;

                    while (!exitExec)
                        ExecOne();
                }
                finally
                {
                    IsRunning = false;
                    exitExec = false;
                }
            }
        }
        private void ExecOne()
        {
            // Check triggers

            if (tickCount > nextRtcIrqTick)
            {
                nextRtcIrqTick += ticksPerIRQ;
                IntMgr.RtcIntLatch.Latch();
            }

            if (waitTrigger.Latched)
            {
                if (tickCount >= waitTimeout ||
                    computer.FloppyControllerDrq ||
                    IntMgr.FdcNmiLatch.Latched || 
                    IntMgr.ResetButtonLatch.Latched)
                {
                    waitTrigger.Unlatch();
                    waitTrigger.ResetTrigger();
                }
            }

            // Execute something

            if (IntMgr.NmiTriggered && z80.CanNmi)
            {
                IntMgr.ResetNmiTriggers();

                z80.NonMaskableInterrupt();
                tickCount += (11 * TICKS_PER_TSTATE);
            }
            else if (waitTrigger.Latched)
            {
                tickCount += TICKS_PER_TSTATE;
            }
            else if (IntMgr.InterruptReq && z80.CanInterrupt)
            {
                IntMgr.RtcIntLatch.ResetTrigger();
                tickCount += z80.Interrupt();
            }
            else
            {
                tickCount += z80.Exec();
            }

            // Do callbacks
            if (tickCount > nextPulseReqTick)
            {
                // descending to avoid problems with new reqs being added
                for (int i = pulseReqs.Count - 1; i >= 0; i--)
                {
                    if (tickCount > pulseReqs[i].Trigger)
                        pulseReqs[i].Execute();
                }
                pulseReqs.RemoveAll(r => r.Inactive);
                SetNextPulseReqTick();
            }
            if (NormalSpeed)
            {
                if (tickCount > nextSoundSampleTick)
                {
                    soundCallback();
                    nextSoundSampleTick += ticksPerSoundSample;
                }
                if (--cyclesUntilNextSyncCheck <= 0)
                {
                    SyncRealTimeOffset();
                    cyclesUntilNextSyncCheck = 250000;   // couple times a second
                }
                else if (--cyclesUntilNextThrottleSleep <= 0)
                {
                    long ticks = 0;
                    NativeMethods.QueryPerformanceCounter(ref ticks);
                    var realTimeTicks = ticks - realTimeElapsedTicksOffset;
                    var virtualTicksReal = realTimeTicks * z80TicksPerRealtimeTick;
                    if (virtualTicksReal < tickCount)
                        System.Threading.Thread.Sleep(MSEC_PER_SLEEP);
                    cyclesUntilNextThrottleSleep = 10;
                }
            }
        }

        private void SyncRealTimeOffset()
        {
            long realTicks = RealTimeTicks;
            long z80EquivalentTicks = (long)(tickCount / z80TicksPerRealtimeTick);
            long newRealTimeElapsedTicksOffset = realTicks - z80EquivalentTicks;

            // if we're already synced to within 100 microseconds don't bother
            if (Math.Abs(newRealTimeElapsedTicksOffset - realTimeElapsedTicksOffset) > 100000)
                realTimeElapsedTicksOffset = newRealTimeElapsedTicksOffset;
        }
        private void SetNextPulseReqTick()
        {
            if (pulseReqs.Count == 0)
                nextPulseReqTick = UInt64.MaxValue;
            else
                nextPulseReqTick = 0;// pulseReqs.Min(r => r.Trigger); <-- causes threading issue 
        }

        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(tickCount);
            Writer.Write(nextRtcIrqTick);
            Writer.Write(exitExec);
            Writer.Write(nextPulseReqTick);
            Writer.Write(nextSoundSampleTick);           
            Writer.Write(waitTimeout);

            waitTrigger.Serialize(Writer);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            tickCount = Reader.ReadUInt64();
            nextRtcIrqTick = Reader.ReadUInt64();
            exitExec = Reader.ReadBoolean();
            nextPulseReqTick = Reader.ReadUInt64();
            nextSoundSampleTick = Reader.ReadUInt64();
            waitTimeout = Reader.ReadUInt64();

            waitTrigger.Deserialize(Reader);
        }
        
        private static double realTimeTicksPerSec;
        private static long realTimeElapsedTicksOffset;
        private static double z80TicksPerRealtimeTick;
        
        private void CalRealTimeClock()
        {
            long rtTicksPerSec = 0;
            NativeMethods.QueryPerformanceFrequency(ref rtTicksPerSec);
            realTimeTicksPerSec = rtTicksPerSec;
            z80TicksPerRealtimeTick = (double)this.ticksPerSec / (double)rtTicksPerSec;
        }
        private static long RealTimeTicks
        {
            get
            {
                long ticks = 0;
                NativeMethods.QueryPerformanceCounter(ref ticks);
                return ticks;
            }
        }
        private void LaunchOnSeparateThread(VoidDelegate Delegate)
        {
            new System.Threading.Thread(new System.Threading.ThreadStart(() => Delegate())).Start();
        }
    }
}
