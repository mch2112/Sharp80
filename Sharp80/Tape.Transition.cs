/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;

namespace Sharp80
{
    internal partial class Tape
    {
        private enum PulseState { Positive, Negative, PositiveClock, NegativeClock, PostClockOne, PostDataOne, PostDataZero, Expired, None }
        private enum PulsePolarity { Positive, Negative, Zero }

        private class Transition : ISerializable
        {
            public delegate bool ReadCallback();

            public PulseState Before { get; private set; }
            public PulseState After { get; private set; }
            public PulseState LastNonZero { get; private set; }

            public bool FlipFlop { get; private set; }

            private static Clock Clock;
            private static ReadCallback Callback { get; set; }

            public bool Value { get; private set; }
            private ulong TimeStamp { get; set; }
            private ulong Duration { get; set; }
            private Baud Speed { get; set; }

            public static void Initialize(Clock Clock, ReadCallback Callback)
            {
                Transition.Clock = Clock;
                Transition.Callback = Callback;
            }

            public Transition(Baud Speed)
            {
                this.Speed = Speed;
                TimeStamp = Clock.TickCount;
                Duration = 0;
            }

            public ulong TicksUntilNext { get { return TimeStamp + Duration - Clock.TickCount; } }
            public bool IsRising
            {
                get
                {
                    return GetPolarity(After) == PulsePolarity.Positive &&
                           GetPolarity(Before) == PulsePolarity.Negative;
                }
            }
            public bool IsFalling
            {
                get
                {
                    return GetPolarity(After) == PulsePolarity.Negative &&
                           GetPolarity(Before) == PulsePolarity.Positive;
                }
            }

            public bool Update(Baud Speed)
            {
                if (this.Speed != Speed)
                {
                    this.Speed = Speed;
                    Before = PulseState.Negative;
                    After = PulseState.Positive;
                    Duration = 0;
                }
                if (Expired)
                {
                    Before = After;
                    TimeStamp += Duration;
                    switch (Speed)
                    {
                        case Baud.High:
                            switch (After)
                            {
                                case PulseState.Positive:
                                    After = PulseState.Negative;
                                    break;
                                case PulseState.Negative:
                                    Value = Callback();
                                    After = PulseState.Positive;
                                    break;
                            }
                            Duration = Value ? HIGH_SPEED_PULSE_ONE : HIGH_SPEED_PULSE_ZERO;
                            break;
                        case Baud.Low:
                            switch (After)
                            {
                                case PulseState.PositiveClock:
                                    After = PulseState.NegativeClock;
                                    Duration = LOW_SPEED_PULSE_NEGATIVE;
                                    break;
                                case PulseState.NegativeClock:
                                    // If zero bit, skip the data pulse
                                    After = Value ? PulseState.PostClockOne : PulseState.PostDataZero;
                                    Duration = Value ? LOW_SPEED_POST_CLOCK_ONE : LOW_SPEED_POST_DATA_ZERO;
                                    break;
                                case PulseState.PostClockOne:
                                    After = PulseState.Positive;
                                    Duration = LOW_SPEED_PULSE_POSITIVE;
                                    break;
                                case PulseState.Positive:
                                    After = PulseState.Negative;
                                    Duration = LOW_SPEED_PULSE_NEGATIVE;
                                    break;
                                case PulseState.Negative:
                                    After = PulseState.PostDataOne;
                                    Duration = LOW_SPEED_POST_DATA_ONE;
                                    break;
                                case PulseState.PostDataOne:
                                case PulseState.PostDataZero:
                                    Value = Callback();
                                    After = PulseState.PositiveClock;
                                    Duration = LOW_SPEED_PULSE_POSITIVE;
                                    break;
                            }
                            break;
                        default:
                            throw new Exception();
                    }
                    if (IsOpposite(LastNonZero, After))
                        FlipFlop = true;
                    if (IsNonZero(After))
                        LastNonZero = After;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            public void ClearFlipFlop()
            {
                FlipFlop = false;
            }

            private bool Expired { get { return Clock.TickCount > Expiration; } }
            private ulong Expiration { get { return TimeStamp + Duration; } }
            private PulsePolarity GetPolarity(PulseState State)
            {
                switch (State)
                {
                    case PulseState.Positive:
                    case PulseState.PositiveClock:
                        return PulsePolarity.Positive;
                    case PulseState.Negative:
                    case PulseState.NegativeClock:
                        return PulsePolarity.Negative;
                    default:
                        return PulsePolarity.Zero;
                }
            }
            private bool IsNonZero(PulseState State)
            {
                return GetPolarity(State) != PulsePolarity.Zero;
            }
            private bool IsOpposite(PulseState State1, PulseState State2)
            {
                var p1 = GetPolarity(State1);
                var p2 = GetPolarity(State2);

                return p1 == PulsePolarity.Negative && p2 == PulsePolarity.Positive ||
                       p2 == PulsePolarity.Negative && p1 == PulsePolarity.Positive;
            }

            // SNAPSHOTS

            public void Serialize(BinaryWriter Writer)
            {
                Writer.Write((int)Speed);
                Writer.Write((int)Before);
                Writer.Write((int)After);
                Writer.Write((int)LastNonZero);
                Writer.Write(FlipFlop);
                Writer.Write(Value);
                Writer.Write(TimeStamp);
                Writer.Write(Duration);
            }
            public bool Deserialize(BinaryReader Reader, int DeserializationVersion)
            {
                try
                {
                    Speed =       (Baud)Reader.ReadInt32();
                    Before =      (PulseState)Reader.ReadInt32();
                    After =       (PulseState)Reader.ReadInt32();
                    LastNonZero = (PulseState)Reader.ReadInt32();
                    FlipFlop =    Reader.ReadBoolean();
                    Value =       Reader.ReadBoolean();
                    TimeStamp =   Reader.ReadUInt64();
                    Duration =    Reader.ReadUInt64();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
