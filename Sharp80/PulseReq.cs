/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal sealed class PulseReq
    {
        public enum DelayBasis { Microseconds, Ticks }

        public ulong Trigger { get; private set; }
        public bool Active { get; private set; }

        private ulong delay;
        private DelayBasis delayBasis;
        private Clock.ClockCallback callback;

        private static ulong ticksPerMillisecond;
        private const ulong MICROSECONDS_PER_MILLISECOND = 1000;
        private const ulong MILLISECONDS_PER_SECOND = 1000;

        public static void SetTicksPerSec(ulong TicksPerSec)
        {
            ticksPerMillisecond = TicksPerSec / MILLISECONDS_PER_SECOND;
        }
        public PulseReq(DelayBasis DelayBasis, ulong Delay, Clock.ClockCallback Callback, bool Active = false)
        {
            delayBasis = DelayBasis;
            delay = Delay;
            callback = Callback;
            this.Active = Active;
        }
        public PulseReq() : this(DelayBasis.Ticks, 0, null, true) { }
        public void Execute()
        {
            if (Active)
            {
                Active = false;
                callback();
            }
        }
        public void SetTrigger(ulong BaselineTicks)
        {
            switch (delayBasis)
            {
                case DelayBasis.Ticks:
                    Trigger = BaselineTicks + delay;
                    break;
                case DelayBasis.Microseconds:
                    Trigger = BaselineTicks + delay * ticksPerMillisecond / MICROSECONDS_PER_MILLISECOND;
                    break;
            }
            Active = true;
        }

        public void Expire() { Active = false; }
        public bool Inactive { get { return !Active; } }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(delay);
            Writer.Write((int)delayBasis);
            Writer.Write(Trigger);
            Writer.Write(Active);
        }
        public void Deserialize(System.IO.BinaryReader Reader, Clock.ClockCallback Callback)
        {
            callback = Callback;

            delay = Reader.ReadUInt64();
            delayBasis = (DelayBasis)Reader.ReadInt32();
            Trigger = Reader.ReadUInt64();
            Active = Reader.ReadBoolean();
        }
    }
}
