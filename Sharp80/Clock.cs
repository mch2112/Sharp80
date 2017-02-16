using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal sealed class Clock : ISerializable
    {
        public event EventHandler ThrottleChanged;
        public delegate void ClockCallback();
        private delegate void VoidDelegate();

        public const ushort TICKS_PER_TSTATE = 1000;
        private const int MSEC_PER_SLEEP = 1;

        // Internal State

        private readonly ulong ticksPerSec;
        private readonly ulong tstatesPerSec;
        private readonly ulong ticksPerIRQ;

        private ulong tickCount;
        private ulong emuSpeedInHz;
        private ulong z80TicksOnLastMeasure;
        private ulong nextEmuSpeedMeasureTicks;
        private ulong nextRtcIrqTick;
#if CASSETTE
        private ulong nextCasIrqTick;
#endif
        private long realTimeTicksOnLastMeasure;
        private ulong waitTimeout;

        private bool throttle;
        private bool exitExec = false;

        private Computer computer;
        private Processor.Z80 z80;

        private List<PulseReq> pulseReqs = new List<PulseReq>();
        private ulong nextPulseReqTick = UInt64.MaxValue;

        private SoundEventCallback soundCallback;
#if CASSETTE
        private Cassette.CassetteReadCallback cassetteCallback;
#endif
        private ulong nextSoundSampleTick;
        private readonly ulong ticksPerSoundSample;

        private Trigger waitTrigger;

        // CONSTRUCTOR

        public Clock(Computer Computer, ulong FrequencyInHz, ulong MilliTStatesPerIRQ, ulong MilliTStatesPerSoundSample, SoundEventCallback SoundCallback, bool Throttle)
        {
            tstatesPerSec = FrequencyInHz;
            ticksPerSec = FrequencyInHz * TICKS_PER_TSTATE;
            ticksPerIRQ = MilliTStatesPerIRQ;

            CalRealTimeClock();

            computer = Computer;
            z80 = computer.Processor;
            
            ticksPerSoundSample = MilliTStatesPerSoundSample;

            tickCount = 0;

            soundCallback = SoundCallback;
            throttle = Throttle;

            PulseReq.SetTicksPerSec(ticksPerSec);

            nextRtcIrqTick = ticksPerIRQ;
#if CASSETTE
            nextCasIrqTick = ulong.MaxValue;
#endif
            ResetTriggers();

            waitTrigger = new Trigger(() => { if (Log.DebugOn) Log.LogToDebug(string.Format("CPU Wait ON")); },
                                      () => { if (Log.DebugOn) Log.LogToDebug(string.Format("CPU Wait OFF")); },
                                      TriggerLock: false,
                                      CanFireOnEnable: false)
                         { Enabled = true };
        }

        // PROPERTIES

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

        public bool Throttle
        {
            get { return throttle; }
            set
            {
                if (throttle != value)
                {
                    ResetTriggers();
                    SyncRealTimeOffset();
                    throttle = value;
                    ThrottleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
#if CASSETTE
        public Cassette.CassetteReadCallback CassetteCallback { set { cassetteCallback = value; } }
#endif
        public void Start()
        {
            if (!IsRunning)
            {
                SyncRealTimeOffset();
                ResetTriggers();
                LaunchOnSeparateThread(Exec);
            }
        }
        public void Stop()
        {
            if (IsRunning)
                exitExec = true;
        }
        public bool IsRunning { get; private set; }

        // PUBLIC METHODS

        public void SingleStep()
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

            if (!pulseReqs.Contains(Req))
                pulseReqs.Add(Req);

            SetNextPulseReqTick();
        }
#if CASSETTE
        public void DoCasIrqNow()
        {
            nextCasIrqTick = tickCount;
        }
#endif
        public string GetInternalsReport(bool IncludeTickCount)
        {
            StringBuilder s = new StringBuilder();

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
            s.Append(((float)emuSpeedInHz / 1000000F).ToString("#0.00") + " MHz ");
            if (IncludeTickCount)
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
            realTimeTicksOnLastMeasure = RealTimeTicks;
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
                computer.IntMgr.RtcIntLatch.Latch();
            }

            if (waitTrigger.Latched)
            {
                if (tickCount >= waitTimeout ||
                    computer.FloppyController.DRQ ||
                    computer.IntMgr.FdcNmiLatch.Latched || 
                    computer.IntMgr.ResetButtonLatch.Latched)
                {
                    waitTrigger.Unlatch();
                    waitTrigger.ResetTrigger();
                }
            }

            // Execute something

            if (computer.IntMgr.NmiTriggered && z80.CanNmi)
            {
                computer.IntMgr.ResetNmiTriggers();

                z80.NonMaskableInterrupt();
                tickCount += (11 * TICKS_PER_TSTATE);
            }
            else if (waitTrigger.Latched)
            {
                tickCount += TICKS_PER_TSTATE;
            }
#if CASSETTE
            else if (tickCount > nextCasIrqTick && !Instruction.CurrentInst.IsPrefix)
            {
                var nextCasIrqDelay = cassetteCallback();

                if (computer.IntMgr.CasIntPending)
                {
                    ulong ticks = z80.Interrupt();
                    if (ticks > 0)
                        tickCount += ticks;
                }
                if (nextCasIrqDelay > 0)
                    if (nextCasIrqTick < ulong.MaxValue)
                        nextCasIrqTick += nextCasIrqDelay * ticksPerSec / 1000000;
                    else
                        nextCasIrqTick = tickCount + nextCasIrqDelay * ticksPerSec / 1000000;
                else
                    nextCasIrqTick = ulong.MaxValue;
            }
#endif
            else if (computer.IntMgr.RtcIntLatch.Triggered && z80.CanInterrupt)
            {
                computer.IntMgr.RtcIntLatch.ResetTrigger();
                tickCount += z80.Interrupt();
            }
            else
            {
                tickCount += z80.Exec();
            }

            // Do callbacks
            if (tickCount > nextPulseReqTick)
            {
                for (int i = pulseReqs.Count - 1; i >= 0; i--) // do this to avoid problems with new reqs being added
                {
                    if (tickCount > pulseReqs[i].Trigger)
                        pulseReqs[i].Execute();
                }
                pulseReqs.RemoveAll(r => r.Expired);
                SetNextPulseReqTick();
            }
            if (throttle)
            {
                if (tickCount > nextSoundSampleTick)
                {
                    soundCallback();
                    nextSoundSampleTick += ticksPerSoundSample;
                }
                if (++skip > 250000)  // couple times a second
                {
                    SyncRealTimeOffset();
                    skip = 0;
                }
                else if (++skip > 10)
                {
                    NativeMethods.QueryPerformanceCounter(ref realTimeElapsedTicks);
                    var realTimeTicks = realTimeElapsedTicks - realTimeElapsedTicksOffset;
                    var virtualTicksReal = realTimeTicks * z80TicksPerRealtimeTick;
                    if (virtualTicksReal < tickCount)
                        System.Threading.Thread.Sleep(MSEC_PER_SLEEP);
                    skip = 0;
                }
            }
        }
        private long skip = 0;
        private void SyncRealTimeOffset()
        {
            NativeMethods.QueryPerformanceCounter(ref realTimeElapsedTicks);
            long equivalentTicks = (long)(tickCount / z80TicksPerRealtimeTick);
            realTimeElapsedTicksOffset = realTimeElapsedTicks - equivalentTicks;
        }
        private void SetNextPulseReqTick()
        {
            if (pulseReqs.Count == 0)
                nextPulseReqTick = UInt64.MaxValue;
            else
                nextPulseReqTick = pulseReqs.Min(r => r.Trigger);
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
        private static long realTimeElapsedTicks;
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
                NativeMethods.QueryPerformanceCounter(ref realTimeElapsedTicks);
                return realTimeElapsedTicks;
            }
        }
        private static double RealTimeSeconds
        {
            get
            {
                NativeMethods.QueryPerformanceCounter(ref realTimeElapsedTicks);
                return ((double)(realTimeElapsedTicks)) / realTimeTicksPerSec;
            }
        }

        private double SafeRatio(ulong Numerator, ulong Denominator)
        {
            return (double)Numerator / (double)Denominator;
        }
        private double SafeRatio(double Numerator, ulong Denominator)
        {
            return Numerator / (double)Denominator;
        }
        private void LaunchOnSeparateThread(VoidDelegate Delegate)
        {
            new System.Threading.Thread(new System.Threading.ThreadStart(() => Delegate())).Start();
        }
    }
}
