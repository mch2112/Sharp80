/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80
{
    internal sealed class PulseReq
    {
        private ulong delay;
        private bool delayBasisIsTicks;
        private ulong trigger;
        private Clock.ClockCallback callback;

        private bool expired = false;
        private static ulong ticksPerMillisecond;
        private const ulong MICROSECONDS_PER_MILLISECOND = 1000;
        private const ulong MILLISECONDS_PER_SECOND = 1000;

        public static void SetTicksPerSec(ulong TicksPerSec)
        {
            ticksPerMillisecond = TicksPerSec / MILLISECONDS_PER_SECOND;
        }
        public PulseReq(ulong DelayInTicks, Clock.ClockCallback Callback)
        {
            delay = DelayInTicks;
            delayBasisIsTicks = true;
            callback = Callback;
            expired = false;
        }
        public PulseReq(ulong DelayInMicroSeconds, Clock.ClockCallback Callback, bool Expired)
        {
            delay = DelayInMicroSeconds;
            delayBasisIsTicks = false;
            callback = Callback;
            expired = Expired;
        }
        public PulseReq() : this(0, null, true) { }
        public void Execute()
        {
            if (!expired)
            {
                expired = true;
                callback();
            }
        }
        public ulong Trigger
        {
            get { return trigger; }
        }
        public void SetTrigger(ulong BaselineTicks)
        {
            if (delayBasisIsTicks)
                trigger = BaselineTicks + delay;
            else
                trigger = BaselineTicks + delay * ticksPerMillisecond / MICROSECONDS_PER_MILLISECOND;
            expired = false;
        }
        public void Expire()
        {
            expired = true;
        }
        public bool Expired
        {
            get { return expired; }
        }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(delay);
            Writer.Write(delayBasisIsTicks);
            Writer.Write(trigger);
            Writer.Write(expired);
        }
        public void Deserialize(System.IO.BinaryReader Reader, Clock.ClockCallback Callback)
        {
            callback = Callback;
            delay = Reader.ReadUInt64();
            delayBasisIsTicks = Reader.ReadBoolean();
            trigger = Reader.ReadUInt64();
            expired = Reader.ReadBoolean();
        }
    }
}
