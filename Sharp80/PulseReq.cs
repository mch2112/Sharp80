/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal sealed class PulseReq
    {
        public enum DelayBasis { Microseconds, Ticks }

        private ulong delay;
        private DelayBasis delayBasis;
        private ulong trigger;
        private Clock.ClockCallback callback;

        private bool active = false;
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
            active = Active;
        }
        public PulseReq() : this(DelayBasis.Ticks, 0, null, true) { }
        public void Execute()
        {
            if (active)
            {
                active = false;
                callback();
            }
        }
        public ulong Trigger
        {
            get { return trigger; }
        }
        public void SetTrigger(ulong BaselineTicks)
        {
            switch (delayBasis)
            {
                case DelayBasis.Ticks:
                    trigger = BaselineTicks + delay;
                    break;
                case DelayBasis.Microseconds:
                    trigger = BaselineTicks + delay * ticksPerMillisecond / MICROSECONDS_PER_MILLISECOND;
                    break;
            }
            active = true;
        }

        public void Expire() { active = false; }
        public bool Active {  get { return active; } }
        public bool Inactive { get { return !active; } }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(delay);
            Writer.Write((int)delayBasis);
            Writer.Write(trigger);
            Writer.Write(active);
        }
        public void Deserialize(System.IO.BinaryReader Reader, Clock.ClockCallback Callback)
        {
            callback = Callback;

            delay = Reader.ReadUInt64();
            delayBasis = (DelayBasis)Reader.ReadInt32();
            trigger = Reader.ReadUInt64();
            active = Reader.ReadBoolean();
        }
    }
}
