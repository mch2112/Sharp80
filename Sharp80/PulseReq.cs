/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal sealed class PulseReq
    {
        internal enum DelayBasis { Microseconds, Ticks }

        public bool Active { get; private set; }
        public ulong Trigger { get; private set; }

        private ulong delay;
        private DelayBasis delayBasis;
        private Clock.ClockCallback callback;

        private const ulong MICROSECONDS_PER_SECOND = 1000000;

        public PulseReq(DelayBasis DelayBasis, ulong Delay, Clock.ClockCallback Callback, bool Active = false)
        {
            delayBasis = DelayBasis;
            delay = Delay;
            callback = Callback;
            this.Active = Active;
        }
        /// <summary>
        /// Empty PulseReq to be deserialized
        /// </summary>
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
                    Trigger = BaselineTicks + delay * Clock.TICKS_PER_SECOND / MICROSECONDS_PER_SECOND;
                    break;
            }
            Active = true;
        }

        public void Expire() => Active = false;
        public bool Inactive => !Active;

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(delay);
            Writer.Write((int)delayBasis);
            Writer.Write(Trigger);
            Writer.Write(Active);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, Clock.ClockCallback Callback, int SerializationVersion)
        {
            try
            {
                callback = Callback;

                delay = Reader.ReadUInt64();
                delayBasis = (DelayBasis)Reader.ReadInt32();
                Trigger = Reader.ReadUInt64();
                Active = Reader.ReadBoolean();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
