using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sharp80
{
    internal enum TapeStatus { Stopped, Reading, ReadEngaged, Writing, WriteEngaged, Waiting }
    internal enum TapeSpeed { Low, High }

    internal class Tape : ISerializable
    {
        private enum PulseState { ReadWrite, Positive, Negative, PositiveClock, NegativeClock, PostClock, PostData, Expired, None}

        private class Pulse
        {
            public TapeSpeed PulseSpeed { get; private set; }
            public ulong Duration { get; private set; } = 0;
            public bool Value { get; private set; }

            public ulong Age { get { return Clock.ElapsedTStates - StartTime; } }

            private ulong StartTime { get; set; }
            private Clock Clock { get; set; }

            public Pulse(TapeSpeed Speed, ulong Duration, Clock Clock)
            {
                PulseSpeed = Speed;
                this.Duration = Duration;
                this.Clock = Clock;
                StartTime = Clock.ElapsedTStates;
            }
            public void Start(bool Value)
            {
                this.Value = Value;
                StartTime = Clock.ElapsedTStates;

                bool dummy = false;

                Advance(ref dummy);
            }
            public void Advance(ref bool FlipFlop)
            {
                var oldType = State;
                switch (PulseSpeed)
                {
                    case TapeSpeed.High:
                        Duration = Value ? 188ul : 376ul;
                        switch (State)
                        {
                            case PulseState.ReadWrite:
                                State = PulseState.Negative;
                                break;
                            case PulseState.Negative:
                                State = PulseState.Positive;
                                break;
                            case PulseState.Positive:
                                State = PulseState.ReadWrite;
                                break;
                            default:
                                throw new Exception();
                        }
                        break;
                    case TapeSpeed.Low:
                        switch (State)
                        {
                            case PulseState.ReadWrite:
                                State = PulseState.PositiveClock;
                                Duration = 128;
                                break;
                            case PulseState.PositiveClock:
                                State = PulseState.NegativeClock;
                                Duration = 128;
                                break;
                            case PulseState.NegativeClock:
                                State = PulseState.PostClock;
                                Duration = Value ? 748ul : (1871ul - 860ul);
                                break;
                            case PulseState.PostClock:
                                if (Value)
                                {
                                    State = PulseState.Positive;
                                    Duration = 128;
                                }
                                else
                                {
                                    State = PulseState.PostData;
                                    Duration = 860ul;
                                }
                                break;
                            case PulseState.Positive:
                                State = PulseState.Negative;
                                Duration = 128;
                                break;
                            case PulseState.Negative:
                                State = PulseState.PostData;
                                Duration = 860ul;
                                break;
                            case PulseState.PostData:
                                State = PulseState.ReadWrite;
                                break;
                            default:
                                throw new Exception();
                        }
                        break;
                }
                if (isNegative(oldType) && isPositive(State))
                    FlipFlop = true;
                if (isPositive(oldType) && isNegative(State))
                    FlipFlop = true;
            }
            private bool isPositive(PulseState Type)
            {
                switch (Type)
                {
                    case PulseState.Positive:
                    case PulseState.PositiveClock:
                        return true;
                    default:
                        return false;
                }
            }
            private bool isNegative(PulseState Type)
            {
                switch (Type)
                {
                    case PulseState.Negative:
                    case PulseState.NegativeClock:
                        return true;
                    default:
                        return false;
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

        public byte Out()
        {
            return flipFlop ? (byte)0x80 : (byte)0x00;
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

        private bool flipFlop = false;

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
            Storage.SaveBinaryFile(Path.Combine(Path.Combine(Storage.AppDataPath, "CAS Files"), "foo.cas"), data);
        }
        public void Play()
        {
            MotorEngaged = true;
            recordInvoked = false;
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
        }
        public void Rewind()
        {
            pulse = new Pulse(Speed, 0, clock);
            bitCursor = 8;
            byteCursor = 0;
            recordInvoked = false;
        }
        private void Update()
        {
            if (pulse == null)
                return;

            switch (Status)
            {
                case TapeStatus.Reading:

                    pulse.Advance(ref flipFlop);

                    if (pulse.State == PulseState.ReadWrite)
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
                    computer.RegisterPulseReq(new PulseReq(pulse.Duration, Update, false));
                    break;

                case TapeStatus.Writing:
                    pulse.Advance(ref flipFlop);

                    if (pulse.State == PulseState.ReadWrite)
                    {
                        bool val = GetWriteVal();
                        pulse.Start(val);
                        Write(val);
                    }
                    computer.RegisterPulseReq(new PulseReq(pulse.Duration, Update, false));
                    break;
            }
        }
        
        private ulong lastWriteZero;
        private ulong lastWritePositive;
        private ulong lastWriteNegative;
        private ulong nextLastWriteZero;
        private ulong nextLastWritePositive;
        private ulong nextLastWriteNegative;

        public void HandleCasPort(byte b)
        {
            if (MotorOn)
            {
                if (pulse == null)
                {
                    switch (Speed)
                    {
                        case TapeSpeed.High:
                            if (b != 1)
                                return;
                            pulse = new Pulse(TapeSpeed.High, 0, clock);
                            Update();
                            break;
                        case TapeSpeed.Low:
                            throw new NotImplementedException();
                    }
                }
                switch (b & 0x03)
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
                        if (lastWritePositive < lastWriteNegative || lastWritePositive < lastWriteZero)
                        {
                            nextLastWritePositive = lastWritePositive;
                            lastWritePositive = clock.ElapsedTStates;
                        }
                        break;
                    case 2:
                        if (lastWriteNegative < lastWritePositive || lastWriteNegative < lastWriteZero)
                        {
                            nextLastWriteNegative = lastWriteNegative;
                            lastWriteNegative = clock.ElapsedTStates;
                        }
                        break;
                }
            }
            flipFlop = false;
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

