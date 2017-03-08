/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80
{
    internal partial class Tape
    {
        private enum PulseState { Positive, Negative, PositiveClock, NegativeClock, PostClock, PostData, Expired, None }
        private enum PulsePolarity { Positive, Negative, Zero }

        private class Transition
        {
            public delegate bool ReadCallback();

            public PulseState Before { get; private set; }
            public PulseState After { get; private set; }
            public bool FlipFlop { get; private set; }
            public ulong TicksUntilNext { get { return TimeStamp + Duration - Clock.TickCount; } }

            private static Clock Clock;
            private static ReadCallback Callback { get; set; }

            public PulseState LastNonZero { get; private set; }
            public bool Value { get; private set; }
            private bool Expired { get { return Clock.TickCount > Expiration; } }

            private ulong TimeStamp { get; set; }
            private ulong Duration { get; set; }
            private ulong Expiration { get { return TimeStamp + Duration; } }
            private Baud Speed { get; set; }

            public Transition(Baud Speed)
            {
                this.Speed = Speed;
                TimeStamp = Clock.TickCount;
                Duration = 0;
            }

            public static void Initialize(Clock Clock, ReadCallback Callback)
            {
                Transition.Clock = Clock;
                Transition.Callback = Callback;
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
                            Duration = Value ? (188ul * 2030) : (376ul * 2030);
                            break;
                        case Baud.Low:
                            switch (After)
                            {
                                case PulseState.PositiveClock:
                                    After = PulseState.NegativeClock;
                                    Duration = 128ul * 2030;
                                    break;
                                case PulseState.NegativeClock:
                                    After = PulseState.PostClock;
                                    Duration = 748 * 2030;
                                    break;
                                case PulseState.PostClock:
                                    After = Value ? PulseState.Positive : PulseState.PostData;
                                    Duration = Value ? 128 * 2030 : (128ul + 128 + 860) * 2030;
                                    break;
                                case PulseState.Positive:
                                    After = PulseState.Negative;
                                    Duration = 128 * 2030;
                                    break;
                                case PulseState.Negative:
                                    After = PulseState.PostData;
                                    Duration = 860 * 2030;
                                    break;
                                case PulseState.PostData:
                                    Value = Callback();
                                    After = PulseState.PositiveClock;
                                    Duration = 128 * 2030;
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
            public bool IsRising
            {
                get
                {
                    return Polarity(After) == PulsePolarity.Positive &&
                           Polarity(Before) == PulsePolarity.Negative;
                }
            }
            public bool IsFalling
            {
                get
                {
                    return Polarity(After) == PulsePolarity.Negative &&
                           Polarity(Before) == PulsePolarity.Positive;
                }
            }
            private bool IsNonZero(PulseState State)
            {
                return Polarity(State) != PulsePolarity.Zero;
            }
            private PulsePolarity Polarity(PulseState State)
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
            private bool IsOpposite(PulseState State1, PulseState State2)
            {
                var p1 = Polarity(State1);
                var p2 = Polarity(State2);

                return p1 == PulsePolarity.Negative && p2 == PulsePolarity.Positive ||
                       p2 == PulsePolarity.Negative && p1 == PulsePolarity.Positive;
            }
        }
    }
}
