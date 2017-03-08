/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.IO;

namespace Sharp80
{
    internal enum TapeStatus { Stopped, Reading, ReadEngaged, Writing, WriteEngaged, Waiting }
    internal enum Baud { Low, High }

    internal partial class Tape : ISerializable
    {
        
        private const int DEFAULT_BLANK_TAPE_LENGTH = 0x0800;
        private const int MAX_TAPE_LENGTH = 0x12000;

        public string FilePath { get; set; }

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

        public string StatusReport
        {
            get
            {
                if (MotorOn)
                {
                    return string.Format(@"{0:0000.0} {1:00.0%} {2} {3}", Counter, Percent, Speed == Baud.High ? "H" : "L", Status);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public string PulseStatus { get { return transition?.After.ToString() ?? PulseState.None.ToString(); } }
        public bool Bit { get { return transition?.Value ?? false; } }
        public byte ReadVal()
        {
            byte ret = 0;

            if (transition?.FlipFlop ?? false)
                ret |= 0x80;

            if ((transition?.LastNonZero ?? PulseState.None) == PulseState.Positive)
                ret |= 0x01;

            return ret;
        }

        public float Counter
        {
            get { return byteCursor + ((7f - bitCursor) / 10); }
        }
        public float Percent
        {
            get { return (float)byteCursor / data.Length; }
        }

        public bool MotorOn
        {
            get { return motorOn; }
            set
            {
                if (motorOn != value)
                {
                    motorOn = value;
                    if (motorOn)
                    {
                        if (!recordInvoked)
                            transition = null;
                        Update();
                    }
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

        private int consecutiveFiftyFives = 0;
        private int consecutiveZeros = 0;

        private Transition transition;

        public Tape(Computer Computer)
        {
            computer = Computer;
        }

        public Baud Speed { get; private set; }
        public bool Changed { get; private set; }

        public void Initialize(Clock Clock, InterruptManager InterruptManager)
        {
            clock = Clock;
            Transition.Initialize(clock, Read);
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

            Stop();

            if (String.IsNullOrWhiteSpace(Path))
            {
                bytes = new byte[DEFAULT_BLANK_TAPE_LENGTH];
            }
            else if (!Storage.LoadBinaryFile(Path, out bytes) || bytes.Length < 0x100)
            {
                bytes = new byte[DEFAULT_BLANK_TAPE_LENGTH];
                return false;
            }
            FilePath = Path;
            InitTape(bytes);
            return true;
        }
        public bool Save()
        {
            if (Storage.SaveBinaryFile(FilePath, data))
            {
                Changed = false;
                return true;
            }
            else
            {
                return false;
            }
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
        }
        public void Stop()
        {
            // test junk

            if (Changed)
            {
                Storage.SaveBinaryFile(@"c:\Users\Matthew\Desktop\foo.cas", data);
                Changed = false;
            }
            MotorEngaged = false;
            recordInvoked = false;
            Rewind();
        }
        public void Eject()
        {
            if (Changed)
            {
                if (!Save())
                    return;
                else
                    Changed = false;
            }
            InitTape();
        }
        public void Rewind()
        {
            bitCursor = 7;
            byteCursor = 0;
            recordInvoked = false;
        }
        private void Update()
        {
            switch (Status)
            {
                case TapeStatus.Reading:

                    transition = transition ?? new Transition(Speed);

                    while (transition.Update(Speed))
                    {
                        if (transition.IsRising)
                            intMgr.CassetteRisingEdgeLatch.Latch();
                        else if (transition.IsFalling)
                            intMgr.CassetteFallingEdgeLatch.Latch();
                    }

                    computer.RegisterPulseReq(new PulseReq(PulseReq.DelayBasis.Ticks,
                                                           transition.TicksUntilNext,
                                                           Update));
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
       
        private int speedHighEvidence = 0;
        private bool skippedLast = false;

        public void HandleCasPort(byte b)
        {
            b &= 0x03;
            if (MotorOn)
            {
                transition?.ClearFlipFlop();

                switch (b)
                {
                    case 0:
                    case 3:
                        if (lastWritePolarity == PulsePolarity.Zero)
                            return;
                        lastWritePolarity = PulsePolarity.Zero;
                        nextLastWriteZero = lastWriteZero;
                        lastWriteZero = clock.TickCount;
                        break;
                    case 1:
                        if (lastWritePolarity == PulsePolarity.Positive)
                            return;
                        lastWritePolarity = PulsePolarity.Positive;
                        nextLastWritePositive = lastWritePositive;
                        lastWritePositive = clock.TickCount;
                        break;
                    case 2:
                        if (lastWritePolarity == PulsePolarity.Negative)
                            return;
                        lastWritePolarity = PulsePolarity.Negative;
                        nextLastWriteNegative = lastWriteNegative;
                        lastWriteNegative = clock.TickCount;
                        break;
                }

                if (Status == TapeStatus.Writing)
                {
                    var posDelta = lastWritePositive - nextLastWritePositive;
                    switch (lastWritePolarity)
                    {
                        case PulsePolarity.Positive:

                            if (posDelta.IsBetween(725000, 801000) ||
                                posDelta.IsBetween(1450000, 1900000))
                            {
                                speedHighEvidence++;
                                if (speedHighEvidence > 8)
                                {
                                    Speed = Baud.High;
                                    speedHighEvidence = Math.Min(speedHighEvidence, 16);
                                    Write(lastWritePositive - nextLastWritePositive < 1100000);
                                }
                            }
                            else if ((posDelta.IsBetween(1920000, 2316000)) ||
                                     (posDelta.IsBetween(3858000, 4484500)))
                            {
                                speedHighEvidence--;

                                if (speedHighEvidence < -8)
                                {
                                    Speed = Baud.Low;
                                    speedHighEvidence = Math.Max(speedHighEvidence, -16);

                                    if (posDelta > 3000000)
                                    {
                                        if (skippedLast)
                                        {
                                            // sync error since we saw a short (clock) last time
                                            // but anything after a short clk is a one
                                            // M3 rom does this when writing the A5 marker in CSAVE (bug?)
                                            Write(true);
                                            skippedLast = false;
                                        }
                                        else
                                        {
                                            // long pulse means we only saw the clock pulse so this is a zero
                                            Write(false);
                                        }
                                    }
                                    else if (skippedLast)
                                    {
                                        // we saw the clock pulse before and now this is data pulse one
                                        skippedLast = false;
                                        Write(true);
                                    }
                                    else
                                    {
                                        // this is the clock pulse, skip it
                                        skippedLast = true;
                                    }
                                }
                            }
                            else
                            {
                                var x = 3;
                            }
                            break;
                    }
                }
            }
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
            {
                byteCursor++;
                bitCursor = 7;
            }
            else
            {
                bitCursor--;
            }
            if (byteCursor >= data.Length)
            {
                if (Status == TapeStatus.Writing && data.Length < MAX_TAPE_LENGTH)
                {
                    Array.Resize(ref data, Math.Min(MAX_TAPE_LENGTH, data.Length * 11 / 10)); // Grow by 10%
                }
                else
                {
                    Stop();
                    return false;
                }
            }
            if (bitCursor == 7 && Status == TapeStatus.Reading)
            {
                switch (data[byteCursor])
                {
                    case 0x55:
                    case 0xAA:
                        consecutiveFiftyFives++;
                        break;
                    case 0x00:
                        consecutiveZeros++;
                        break;
                    default:
                        consecutiveFiftyFives = 0;
                        consecutiveZeros = 0;
                        break;
                }
                if (consecutiveFiftyFives > 20)
                    Speed = Baud.High;
                else if (consecutiveZeros > 20)
                    Speed = Baud.Low;
            }
            return true;
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
                Changed = true;
            }
        }
        private void InitTape(byte[] Bytes = null)
        {
            if (Bytes == null)
                FilePath = String.Empty;

            data = Bytes ?? new byte[DEFAULT_BLANK_TAPE_LENGTH];
            Rewind();
            ResetWriteHistory();
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
