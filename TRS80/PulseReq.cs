/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    internal class PulseReq
    {
        public enum DelayBasis { Microseconds, Ticks }

        public bool Active { get; private set; }
        public ulong Trigger { get; private set; } = ulong.MaxValue;

        private ulong delay;
        private DelayBasis delayBasis;
        private Action callback;

        private const ulong MICROSECONDS_PER_SECOND = 1000000;

        public PulseReq(DelayBasis DelayBasis, ulong Delay, Action Callback)
        {
            delayBasis = DelayBasis;
            delay = Delay;
            callback = Callback;
            Active = false;
        }
        /// <summary>
        /// Empty PulseReq to be deserialized
        /// </summary>
        public PulseReq() : this(DelayBasis.Ticks, 0, null) { }
        public void Execute()
        {
            if (Active)
            {
                Active = false;
                Trigger = ulong.MaxValue;
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
        public bool Deserialize(System.IO.BinaryReader Reader, Action Callback, int SerializationVersion)
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
