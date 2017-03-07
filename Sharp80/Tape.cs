using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sharp80
{
    internal enum TapeStatus { Stopped, Reading, ReadEngaged, Writing, WriteEngaged, Waiting }
    internal enum TapeSpeed { Low, High, Unknown }

    internal class Tape : ISerializable
    {
        private enum PulseState { Positive, Negative, PositiveClock, NegativeClock, PostClock, PostData, Expired, None}
        private enum PulsePolarity {  Positive, Negative, Zero }
        private class Pulse
        {
            public TapeSpeed PulseSpeed { get; private set; }
            public bool Value { get; private set; }

            public ulong Age { get { return (Clock.ElapsedTStates - StartTime) * 1000000 * Clock.TICKS_PER_TSTATE / Clock.TicksPerSec; } }

            private ulong StartTime { get; set; }
            private static Clock Clock { get; set; }
            public bool FlipFlop { get; private set; }
            private PulseState PrevNonZeroState { get; set; } = PulseState.None;
            public static void Initialize(Clock Clock)
            {
                Pulse.Clock = Clock;
            }
            public Pulse(TapeSpeed Speed)
            {
                PulseSpeed = Speed;
                StartTime = 0;
            }
            public void Start(bool Value)
            {
                if (StartTime == 0)
                    StartTime = Clock.ElapsedTStates;
                else
                    StartTime += Duration * Clock.TicksPerSec / Clock.TICKS_PER_TSTATE / 1000000;

                this.Value = Value;
            }
            public void ClearFlipFlop()
            {
                FlipFlop = false;
            }
            public PulseState State
            {
                get
                {
                    var s = _State;

                    if (IsOpposite(PrevNonZeroState, s))

                        FlipFlop = true;

                    if (IsNonZero(s))
                        PrevNonZeroState = s;

                    return s;
                }
            }
            private PulseState _State
            {
                get
                {
                    ulong age = Age;
                    switch (PulseSpeed)
                    {
                        case TapeSpeed.High:
                            if (Value)
                            {
                                if (age < 188)
                                    return PulseState.Negative;
                                else if (age < 188 * 2)
                                    return PulseState.Positive;
                                else
                                    return PulseState.Expired;
                            }
                            else
                            {
                                if (age < 376)
                                    return PulseState.Negative;
                                else if (age < 376 * 2)
                                    return PulseState.Positive;
                                else
                                    return PulseState.Expired;
                            }
                        case TapeSpeed.Low:
                            if (age < 128)
                                return PulseState.PositiveClock;
                            else if (age < 128 + 128)
                                return PulseState.NegativeClock;
                            else if (age < 128 + 128 + 748)
                                return PulseState.PostClock;
                            else if (age > 128 + 128 + 748 + 128 + 128 + 860)
                                return PulseState.Expired;
                            else if (Value)
                            {
                                if (age < 128 + 128 + 748 + 128)
                                    return PulseState.Positive;
                                if (age < 128 + 128 + 748 + 128 + 128)
                                    return PulseState.Negative;
                                else
                                    return PulseState.PostData;
                            }
                            else
                            {
                                return PulseState.PostData;
                            }
                    }
                    throw new Exception();
                }
            }
            public ulong TimeUntilNextTransition
            {
                get
                {
                    switch (State)
                    {
                        case PulseState.Negative:
                            if (PulseSpeed == TapeSpeed.High)
                                return (Value ? 188ul : 376ul) - Age;
                            else
                                return Duration - 860 - Age;
                        case PulseState.Positive:
                            if (PulseSpeed == TapeSpeed.High)
                                return (Value ? 188ul : 376ul) * 2 - Age;
                            else
                                return Duration - 860 - 128 - Age;
                        case PulseState.PositiveClock:
                            return 128 - Age;
                        case PulseState.NegativeClock:
                            return 128 + 128 - Age;
                        case PulseState.PostClock:
                            return 128 + 128 + 748 - Age;
                        case PulseState.PostData:
                            return Duration - Age;
                        case PulseState.None:
                        case PulseState.Expired:
                            return 0;
                        default:
                            throw new Exception();
                    }
                }
            }
            public ulong Duration
            {
                get
                {
                    switch (this.PulseSpeed)
                    {
                        case TapeSpeed.High:
                            return Value ? 188ul * 2 : 376ul * 2;
                        case TapeSpeed.Low:
                            return 128 + 128 + 748 + 128 + 128 + 860;
                        default:
                            throw new Exception();
                    }
                }
            }
            public ulong StateDuration
            {
                get
                {
                    switch (PulseSpeed)
                    {
                        case TapeSpeed.High:
                            switch (State)
                            {
                                case PulseState.Positive:
                                case PulseState.Negative:
                                    return Value ? 188ul : 376ul;
                                case PulseState.Expired:
                                    return 0;
                                default:
                                    throw new Exception();
                            }
                        case TapeSpeed.Low:
                            switch (State)
                            {
                                case PulseState.PositiveClock:
                                case PulseState.NegativeClock:
                                case PulseState.Positive:
                                case PulseState.Negative:
                                    return 128;
                                case PulseState.PostClock:
                                    return 748;
                                case PulseState.PostData:
                                    return Value ? 860ul : 128 + 128 + 860;
                                case PulseState.Expired:
                                    return 0;
                                default:
                                    throw new Exception();
                            }
                        default:
                            throw new Exception();
                    }
                }
            }
            private bool IsOpposite(PulseState State1, PulseState State2)
            {
                var p1 = Polarity(State1);
                var p2 = Polarity(State2);

                return p1 == PulsePolarity.Negative && p2 == PulsePolarity.Positive ||
                       p2 == PulsePolarity.Negative && p1 == PulsePolarity.Positive;
            }
            private bool IsNonZero(PulseState State)
            {
                return Polarity(State) != PulsePolarity.Zero;
            }
            public PulsePolarity Polarity(PulseState State)
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
        } 

        public string FilePath { get; private set; }
        public TimeSpan Time { get { return TimeSpan.Zero; } }
        public TapeStatus Status
        {
            get
            {
                if (MotorOn)
                    return recordInvoked ? TapeStatus.Writing : TapeStatus.Reading;
                else if (MotorEngaged)
                    return recordInvoked ? TapeStatus.WriteEngaged : TapeStatus.ReadEngaged;
                else if (MotorOnSignal)
                    return TapeStatus.Waiting;
                else
                    return TapeStatus.Stopped;
            }
        }
        public string PulseStatus
        {
            get { return pulse?.State.ToString() ?? PulseState.None.ToString(); }
        }
        public byte FlipFlopVal()
        {
            return (pulse?.FlipFlop ?? false) ? (byte)0x80 : (byte)0x00;
        }
        public TapeSpeed Speed { get; private set; }
        public float Counter
        {
            get
            {
                return byteCursor + ((7f - bitCursor) / 10);
            }
        }

        public bool MotorOn
        {
            get { return motorOn; }
            set
            {
                if (motorOn != value)
                {
                    motorOn = value;
                    pulse = null;
                    if (motorOn)
                        Update();
                }
            }
        }
        public bool MotorEngaged
        {
            get { return motorEngaged; }
            private set
            {
                motorEngaged = value;
                MotorOn = MotorEngaged && MotorOnSignal;
            }
        }
        public bool MotorOnSignal
        {
            get { return motorOnSignal; }
            set
            {
                motorOnSignal = value;
                MotorOn = MotorEngaged && MotorOnSignal;
            }
        }

        private Computer computer;
        private InterruptManager intMgr;
        private Clock clock;

        private byte[] data;
        private int byteCursor;
        private byte bitCursor;

        private bool motorOn = false;
        private bool motorOnSignal = false;
        private bool motorEngaged = false;

        private bool recordInvoked = false;

        private Pulse pulse;

        public Tape(Computer Computer)
        {
            computer = Computer;
        }
        public void Initialize(Clock Clock, InterruptManager InterruptManager)
        {
            clock = Clock;
            Pulse.Initialize(clock);
            intMgr = InterruptManager;
            InitTape();
        }
        public bool LoadBlank()
        {
            return Load(String.Empty);
        }
        public bool Load(string Path)
        {
            byte[] bytes;
            if (String.IsNullOrWhiteSpace(Path))
                bytes = new byte[0x20000];
            else
                bytes = Storage.LoadBinaryFile(Path);

            if (bytes.Length > 0)
            {
                FilePath = Path;
                InitTape(bytes);
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Save()
        {
            Storage.SaveBinaryFile(@"C:\Users\Matthew\Desktop\foo.cas", data);
        }
        public void Play()
        {
            MotorEngaged = true;
            recordInvoked = false;
            pulse = null;
        }
        public void Record()
        {
            MotorEngaged = true;
            recordInvoked = true;
            Speed = TapeSpeed.High;
            pulse = null;
        }
        public void Stop()
        {
            MotorEngaged = false;
            recordInvoked = false;
            pulse = null;
        }
        public void Rewind()
        {
            pulse = null;
            bitCursor = 8;
            byteCursor = 0;
            recordInvoked = false;
        }
        private void Update()
        {
            switch (Status)
            {
                case TapeStatus.Reading:

                    pulse = pulse ?? new Pulse(Speed);

                    if (pulse.State == PulseState.Expired)
                        pulse.Start(Read());
                    
                    switch (pulse.State)
                    {
                        case PulseState.Positive:
                        case PulseState.PositiveClock:
                            intMgr.CassetteRisingEdgeLatch.Latch();
                            break;
                        case PulseState.Negative:
                        case PulseState.NegativeClock:
                            intMgr.CassetteFallingEdgeLatch.Latch();
                            break;
                    }
                    computer.RegisterPulseReq(new PulseReq(pulse.TimeUntilNextTransition, Update, false));
                    break;
            }
        }
        
        private ulong lastWriteZero;
        private ulong lastWritePositive;
        private ulong lastWriteNegative;
        private ulong nextLastWriteZero;
        private ulong nextLastWritePositive;
        private ulong nextLastWriteNegative;
        private PulsePolarity lastWritePolarity = PulsePolarity.Zero;

        public void HandleCasPort(byte b)
        {
            b &= 0x03;
            if (MotorOn)
            {
                switch (b)
                {
                    case 0:
                    case 3:
                        if (lastWriteZero < lastWritePositive || lastWriteZero < lastWriteNegative)
                        {
                            nextLastWriteZero = lastWriteZero;
                            lastWriteZero = clock.ElapsedTStates;
                        }
                        break;
                    case 1:
                        lastWritePolarity = PulsePolarity.Positive;
                        if (lastWritePositive < lastWriteNegative || lastWritePositive < lastWriteZero)
                        {
                            nextLastWritePositive = lastWritePositive;
                            lastWritePositive = clock.ElapsedTStates;
                        }
                        break;
                    case 2:
                        lastWritePolarity = PulsePolarity.Negative;
                        if (lastWriteNegative < lastWritePositive || lastWriteNegative < lastWriteZero)
                        {
                            nextLastWriteNegative = lastWriteNegative;
                            lastWriteNegative = clock.ElapsedTStates;
                        }
                        break;
                }
                switch (Speed)
                {
                    case TapeSpeed.High:
                        if (lastWritePolarity == PulsePolarity.Positive)
                            Write(GetWriteVal());
                        break;
                    case TapeSpeed.Low:
                        throw new NotImplementedException();
                }
            }
            pulse?.ClearFlipFlop();
        }
        public void Deserialize(BinaryReader Reader)
        {
            throw new NotImplementedException();
        }
        public void Serialize(BinaryWriter Writer)
        {
            throw new NotImplementedException();
        }
        private bool AdvanceCursor()
        {
            if (bitCursor == 0)
                { byteCursor++; bitCursor = 7; }
            else
                { bitCursor--; }

            if (byteCursor >= data.Length)
            {
                Stop();
                return false;
            }
            else
            {
                return true;
            }
        }
        private bool Read()
        {
            if (AdvanceCursor())
            {
                return data[byteCursor].IsBitSet(bitCursor);
            }
            else
            {
                return false;
            }
        }
        private void Write(bool Value)
        {
            if (AdvanceCursor())
            {
                if (Value)
                    data[byteCursor] = data[byteCursor].SetBit(bitCursor);
                else
                    data[byteCursor] = data[byteCursor].ResetBit(bitCursor);
            }
        }
        private void InitTape(byte[] Bytes = null)
        {
            data = Bytes ?? new byte[0x20000];
            
            if (Bytes == null ||
                data.Take(0x100).Count(b => b == 0x55) > 0x30 ||
                data.Take(0x100).Count(b => b == 0xFF) > 0x30)
                Speed = TapeSpeed.High;
            else
                Speed = TapeSpeed.Low;

            ResetWriteHistory();
            Rewind();
        }
        private bool GetWriteVal()
        {
            switch (Speed)
            {
                case TapeSpeed.High:
                    //if (lastWritePositive > lastWriteNegative)
                        return lastWritePositive - nextLastWritePositive < 1200;
                    //else
                      //  return lastWriteNegative - nextLastWriteNegative < 1200;
                case TapeSpeed.Low:
                default:
                    return lastWriteZero - nextLastWriteZero > 1300;
            }
        }
        private void ResetWriteHistory()
        {
            lastWriteZero = 100;
            lastWritePositive = 101;
            lastWriteNegative = 102;
            nextLastWriteZero = 0;
            nextLastWritePositive = 1;
            nextLastWriteNegative = 2;
        }
    }
}

