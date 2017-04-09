/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    internal partial class Tape
    {
        private enum PulseState { Positive, Negative, PositiveClock, NegativeClock, PostClockOne, PostDataOne, PostDataZero, Expired, None }
        private enum PulsePolarity { Positive, Negative, Zero }

        private class Transition : ISerializable
        {
            public PulseState Before { get; private set; }
            public PulseState After { get; private set; }
            public PulseState LastNonZero { get; private set; }

            public bool Value { get; private set; }
            public bool FlipFlop { get; private set; }

            private Clock clock;
            private Func<bool> callback;
            private ulong timeStamp;
            private ulong duration;
            private Baud speed;

            public Transition(Baud Speed, Clock Clock, Func<bool> Callback)
            {
                speed = Speed;
                clock = Clock;
                callback = Callback;
                timeStamp = Clock.TickCount;
                duration = 0;
            }

            public ulong TicksUntilNext => timeStamp + duration - clock.TickCount;
            public bool IsRising => GetPolarity(After) == PulsePolarity.Positive && GetPolarity(Before) == PulsePolarity.Negative;
            public bool IsFalling => GetPolarity(After) == PulsePolarity.Negative && GetPolarity(Before) == PulsePolarity.Positive;

            public bool Update(Baud Speed)
            {
                if (speed != Speed)
                {
                    speed = Speed;
                    Before = PulseState.Negative;
                    After = PulseState.Positive;
                    duration = 0;
                }
                if (Expired)
                {
                    Before = After;
                    timeStamp += duration;
                    switch (speed)
                    {
                        case Baud.High:
                            switch (After)
                            {
                                case PulseState.Positive:
                                    After = PulseState.Negative;
                                    break;
                                case PulseState.Negative:
                                    Value = callback();
                                    After = PulseState.Positive;
                                    break;
                            }
                            duration = Value ? HIGH_SPEED_PULSE_ONE : HIGH_SPEED_PULSE_ZERO;
                            break;
                        case Baud.Low:
                            switch (After)
                            {
                                case PulseState.PositiveClock:
                                    After = PulseState.NegativeClock;
                                    duration = LOW_SPEED_PULSE_NEGATIVE;
                                    break;
                                case PulseState.NegativeClock:
                                    // If zero bit, skip the data pulse
                                    After = Value ? PulseState.PostClockOne : PulseState.PostDataZero;
                                    duration = Value ? LOW_SPEED_POST_CLOCK_ONE : LOW_SPEED_POST_DATA_ZERO;
                                    break;
                                case PulseState.PostClockOne:
                                    After = PulseState.Positive;
                                    duration = LOW_SPEED_PULSE_POSITIVE;
                                    break;
                                case PulseState.Positive:
                                    After = PulseState.Negative;
                                    duration = LOW_SPEED_PULSE_NEGATIVE;
                                    break;
                                case PulseState.Negative:
                                    After = PulseState.PostDataOne;
                                    duration = LOW_SPEED_POST_DATA_ONE;
                                    break;
                                case PulseState.PostDataOne:
                                case PulseState.PostDataZero:
                                    Value = callback();
                                    After = PulseState.PositiveClock;
                                    duration = LOW_SPEED_PULSE_POSITIVE;
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

            private bool Expired => clock.TickCount > Expiration;
            private ulong Expiration => timeStamp + duration;
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
            private bool IsNonZero(PulseState State) => GetPolarity(State) != PulsePolarity.Zero;
            private bool IsOpposite(PulseState State1, PulseState State2)
            {
                var p1 = GetPolarity(State1);
                var p2 = GetPolarity(State2);

                return p1 == PulsePolarity.Negative && p2 == PulsePolarity.Positive ||
                       p2 == PulsePolarity.Negative && p1 == PulsePolarity.Positive;
            }

            // SNAPSHOTS

            public void Serialize(System.IO.BinaryWriter Writer)
            {
                Writer.Write((int)speed);
                Writer.Write((int)Before);
                Writer.Write((int)After);
                Writer.Write((int)LastNonZero);
                Writer.Write(FlipFlop);
                Writer.Write(Value);
                Writer.Write(timeStamp);
                Writer.Write(duration);
            }
            public bool Deserialize(System.IO.BinaryReader Reader, int DeserializationVersion)
            {
                try
                {
                    speed = (Baud)Reader.ReadInt32();
                    Before = (PulseState)Reader.ReadInt32();
                    After = (PulseState)Reader.ReadInt32();
                    LastNonZero = (PulseState)Reader.ReadInt32();
                    FlipFlop = Reader.ReadBoolean();
                    Value = Reader.ReadBoolean();
                    timeStamp = Reader.ReadUInt64();
                    duration = Reader.ReadUInt64();
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
