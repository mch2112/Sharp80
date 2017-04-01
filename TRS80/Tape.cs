/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Linq;

namespace Sharp80.TRS80
{
    public enum TapeStatus { Stopped, Reading, ReadEngaged, Writing, WriteEngaged, Waiting }
    public enum Baud { Low, High }

    internal partial class Tape : ISerializable
    {
        private const int DEFAULT_BLANK_TAPE_LENGTH = 0x0800;
        private const int MAX_TAPE_LENGTH = 0x12000;

        // Values in ticks (1 tstate = 1000 ticks)
        // All determined empirically by M3 ROM write timing
        // Ranges and thresholds are positive to positive, so about twice
        // the single pulse duration
        private const ulong HIGH_SPEED_PULSE_ONE = 378000;
        private const ulong HIGH_SPEED_PULSE_ZERO = 771000;
        private const ulong HIGH_SPEED_ONE_DELTA_RANGE_MIN = 721000;
        private const ulong HIGH_SPEED_ONE_DELTA_RANGE_MAX = 797000;
        private const ulong HIGH_SPEED_THRESHOLD = 1200000;
        private const ulong HIGH_SPEED_ZERO_DELTA_RANGE_MIN = 1459000;
        private const ulong HIGH_SPEED_ZERO_DELTA_RANGE_MAX = 1860000;
        private const ulong HIGH_SPEED_ZERO_DELTA_OUTLIER_MIN = 3660000;
        private const ulong HIGH_SPEED_ZERO_DELTA_OUTLIER_MAX = 3885999;

        private const ulong LOW_SPEED_PULSE_NEGATIVE = 203000;
        private const ulong LOW_SPEED_PULSE_POSITIVE = 189000;
        private const ulong LOW_SPEED_POST_CLOCK_ONE = 1632000;
        private const ulong LOW_SPEED_POST_DATA_ONE = 1632000;
        private const ulong LOW_SPEED_POST_DATA_ZERO = 3669000;
        private const ulong LOW_SPEED_ONE_DELTA_RANGE_MIN = 1923000;
        private const ulong LOW_SPEED_ONE_DELTA_RANGE_MAX = 2281000;
        private const ulong LOW_SPEED_THRESHOLD = 3000000;
        private const ulong LOW_SPEED_ZERO_DELTA_RANGE_MIN = 3886000;
        private const ulong LOW_SPEED_ZERO_DELTA_RANGE_MAX = 4379000;

        private Computer computer;
        private InterruptManager intMgr;
        private Clock clock;
        private PulseReq readPulseReq = null;

        public string FilePath { get; set; }

        private byte[] data;
        private int byteCursor;
        private byte bitCursor;
        private bool isBlank;

        private bool motorOn = false;
        private bool motorOnSignal = false;
        private bool motorEngaged = false;
        private bool recordInvoked = false;

        private int consecutiveFiftyFives = 0;
        private int consecutiveZeros = 0;

        private ulong lastWritePositive;
        private ulong nextLastWritePositive;
        private PulsePolarity lastWritePolarity = PulsePolarity.Zero;

        private int highSpeedWriteEvidence = 0;
        private bool skippedLast = false;

        private Transition transition;

        public Baud Speed { get; private set; }
        public bool Changed { get; private set; }
        public float Counter => byteCursor + ((7f - bitCursor) / 10);
        public float Percent => (float)byteCursor / data.Length;

        // CONSTRUCTOR

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

        // OUTPUT

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
                    return string.Format(@"{0:0000.0} {1:00.0%} {2} {3}", Counter, Percent, recordInvoked ? "Wr" : "Rd", Speed == Baud.High ? "H" : "L");
                }
                else if (MotorOnSignal)
                {
                    return string.Format(@"{0:0000.0} {1:00.0%} {2} Wait", Counter, Percent);
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        public string PulseStatus { get { return transition?.After.ToString() ?? String.Empty; } }
        public byte Value
        {
            get
            {
                byte ret = 0;
                if (transition?.FlipFlop ?? false)
                    ret |= 0x80;
                if ((transition?.LastNonZero ?? PulseState.None) == PulseState.Positive)
                    ret |= 0x01;
                return ret;
            }
        }
        public bool IsBlank { get { return isBlank; } }

        // MOTOR CONTROL

        public bool MotorOn
        {
            get => motorOn;
            set
            {
                if (motorOn != value)
                {
                    motorOn = value;
                    transition = null;
                    if (motorOn)
                        Update();
                }
            }
        }
        public bool MotorEngaged
        {
            get => motorEngaged;
            private set
            {
                motorEngaged = value;
                MotorOn = MotorEngaged && MotorOnSignal;
            }
        }
        public bool MotorOnSignal
        {
            get => motorOnSignal;
            set
            {
                motorOnSignal = value;
                MotorOn = MotorEngaged && MotorOnSignal;
            }
        }

        // USER CONTROLS

        public bool LoadBlank() => Load(String.Empty);

        public bool Load(string Path)
        {
            Stop();

            byte[] bytes;
            FilePath = Path;
            if (String.IsNullOrWhiteSpace(Path) || Storage.IsFileNameToken(FilePath))
            {
                // init tape will take care of it
                bytes = null;
            }
            else if (!IO.LoadBinaryFile(Path, out bytes) || bytes.Length < 0x100)
            {
                return false;
            }
            InitTape(bytes);
            return true;
        }
        public bool Save()
        {
            // Leave at most 0x100 trailing zeros in file
            int i = data.Length - 1;
            while (i > 0x100 && data[i] == 0)
                i--;
            i += 0x100;
            if (data.Length > i)
                Array.Resize(ref data, i);

            if (IO.SaveBinaryFile(FilePath, data))
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
            MotorEngaged = false;
            recordInvoked = false;
        }
        public void Eject() => InitTape();

        public void Rewind()
        {
            bitCursor = 7;
            byteCursor = 0;
            recordInvoked = false;
            Stop();
        }

        // TAPE SETUP

        private void InitTape(byte[] Bytes = null)
        {
            if (Bytes == null)
            {
                FilePath = Storage.FILE_NAME_NEW;
                data = new byte[DEFAULT_BLANK_TAPE_LENGTH];
                isBlank = true;
                Speed = computer.TapeUserSelectedSpeed; // if we write, this is what we'll write at
            }
            else
            {
                data = Bytes;
                isBlank = data.All(b => b == 0x00);
                GuessSpeed();
            }
            Rewind();
            lastWritePositive = 1;
            nextLastWritePositive = 0;
        }

        // CURSOR CONTROL

        /// <summary>
        /// Move the cursor down the tape by one bit. If reading and a new byte is encountered,
        /// check to see if it indicates header values for high or low speed. Low speed headers
        /// are usually zeros, and high speed headers are usually 0x55 (or 0xAA if offset by a
        /// single bit)
        /// </summary>
        /// <returns></returns>
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
                // When writing, we can dynamically resize the tape length up to a reasonable amount
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
                        consecutiveZeros = 0;
                        break;
                    case 0x00:
                        consecutiveZeros++;
                        consecutiveFiftyFives = 0;
                        break;
                    default:
                        consecutiveFiftyFives = 0;
                        consecutiveZeros = 0;
                        break;
                }
                if (consecutiveFiftyFives > 10)
                    Speed = Baud.High;
                else if (consecutiveZeros > 20)
                    Speed = Baud.Low;
            }
            return true;
        }

        private void GuessSpeed()
        {
            if (IsBlank)
            {
                Speed = computer.TapeUserSelectedSpeed;
            }
            else
            {
                int consecutiveFiftyFives = 0;

                for (int i = 0; i < data.Length; i++)
                    switch (data[i])
                    {
                        case 0x00:
                            break;
                        case 0x55:
                        case 0xAA:
                            if (++consecutiveFiftyFives > 20)
                            {
                                Speed = Baud.High;
                                return;
                            }
                            break;
                        default:
                            Speed = Baud.Low;
                            return;
                    }
                Speed = Baud.Low;
            }
        }

        // READ OPERATIONS

        /// <summary>
        /// Keep checking the transitions when reading and raise appropriate
        /// interrupts.
        /// </summary>
        private void Update()
        {
            if (Status == TapeStatus.Reading)
            {
                // capture transition in t in case transition is nullified
                // asynchronously
                var t = transition = transition ?? new Transition(Speed, clock, Read);
                while (t.Update(Speed))
                {
                    if (t.IsRising) intMgr.CasRisingEdgeIntLatch.Latch();
                    else if (t.IsFalling) intMgr.CasFallingEdgeIntLatch.Latch();
                }
                // Keep coming back as long as we're in read status
                readPulseReq?.Expire();
                computer.Activate(readPulseReq = new PulseReq(PulseReq.DelayBasis.Ticks,
                                                                      t.TicksUntilNext,
                                                                      Update));
            }
        }
        private bool Read() => AdvanceCursor() && data[byteCursor].IsBitSet(bitCursor);

        // WRITE OPERATIONS

        public void WriteToCasPort(byte b)
        {
            if (MotorOn)
            {
                transition?.ClearFlipFlop();
                var polarity = GetPolarity(b);
                if (lastWritePolarity == polarity)
                    return;
                lastWritePolarity = polarity;

                // Write values are based on the time difference between consecutive
                // rising pulses
                if (polarity == PulsePolarity.Positive)
                {
                    nextLastWritePositive = lastWritePositive;
                    lastWritePositive = clock.TickCount;

                    if (Status == TapeStatus.Writing)
                    {
                        // Check to see, are the pulse lengths within 5% of
                        // those written by trs80 rom routines?
                        var posDelta = lastWritePositive - nextLastWritePositive;
                        if (posDelta.IsBetween(HIGH_SPEED_ONE_DELTA_RANGE_MIN, HIGH_SPEED_ONE_DELTA_RANGE_MAX) ||
                            posDelta.IsBetween(HIGH_SPEED_ZERO_DELTA_RANGE_MIN, HIGH_SPEED_ZERO_DELTA_RANGE_MAX))
                        {
                            // This is a high speed pulse
                            if (++highSpeedWriteEvidence > 8)
                            {
                                Speed = Baud.High;
                                highSpeedWriteEvidence = Math.Min(highSpeedWriteEvidence, 16);
                                Write(posDelta < HIGH_SPEED_THRESHOLD);
                            }
                        }
                        else if ((posDelta.IsBetween(LOW_SPEED_ONE_DELTA_RANGE_MIN, LOW_SPEED_ONE_DELTA_RANGE_MAX)) ||
                                 (posDelta.IsBetween(LOW_SPEED_ZERO_DELTA_RANGE_MIN, LOW_SPEED_ZERO_DELTA_RANGE_MAX)))
                        {
                            // This is a low speed pulse
                            if (--highSpeedWriteEvidence < -8)
                            {
                                Speed = Baud.Low;
                                highSpeedWriteEvidence = Math.Max(highSpeedWriteEvidence, -16);
                                if (posDelta > LOW_SPEED_THRESHOLD)
                                {
                                    if (skippedLast)
                                    {
                                        // sync error since we saw a short (clock) last time
                                        // but anything after a short clk is a one
                                        // M3 ROM does this when writing the A5 marker in CSAVE
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
                            // we have an outlier pulse length
                            if (Speed == Baud.High && posDelta.IsBetween(HIGH_SPEED_ZERO_DELTA_OUTLIER_MIN, HIGH_SPEED_ZERO_DELTA_OUTLIER_MAX))
                            {
                                // Delay that M3 Rom puts after 0x7F sync byte, accept as a valid zero
                                Write(false);
                            }
                        }
                    }
                }
            }
        }
        private PulsePolarity GetPolarity(byte Input)
        {
            switch (Input & 0x03)
            {
                case 1:
                    return PulsePolarity.Positive;
                case 2:
                    return PulsePolarity.Negative;
                default:
                    return PulsePolarity.Zero;
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
                isBlank &= !Value;
            }
        }

        // MISC

        // SNAPSHOT SUPPORT

        public void Serialize(BinaryWriter Writer)
        {
            Writer.Write((int)Speed);
            Writer.Write(Changed);
            Writer.Write(FilePath);
            Writer.Write(data.Length);
            Writer.Write(data);
            Writer.Write(isBlank);
            Writer.Write(byteCursor);
            Writer.Write(bitCursor);
            Writer.Write(motorOn);
            Writer.Write(motorOnSignal);
            Writer.Write(motorEngaged);
            Writer.Write(recordInvoked);
            Writer.Write(consecutiveFiftyFives);
            Writer.Write(consecutiveZeros);
            Writer.Write(lastWritePositive);
            Writer.Write(nextLastWritePositive);
            Writer.Write((int)lastWritePolarity);
            Writer.Write(highSpeedWriteEvidence);
            Writer.Write(skippedLast);
            Writer.Write(transition != null);
            if (transition != null)
                transition.Serialize(Writer);
            Writer.Write(readPulseReq != null);
            if (readPulseReq != null)
                readPulseReq.Serialize(Writer);
        }
        public bool Deserialize(BinaryReader Reader, int SerializationVersion)
        {
            try
            {
                bool ok = true;

                Speed = (Baud)Reader.ReadInt32();
                Changed = Reader.ReadBoolean();
                FilePath = Reader.ReadString();
                data = Reader.ReadBytes(Reader.ReadInt32());
                isBlank = Reader.ReadBoolean();
                byteCursor = Reader.ReadInt32();
                bitCursor = Reader.ReadByte();
                motorOn = Reader.ReadBoolean();
                motorOnSignal = Reader.ReadBoolean();
                motorEngaged = Reader.ReadBoolean();
                recordInvoked = Reader.ReadBoolean();
                consecutiveFiftyFives = Reader.ReadInt32();
                consecutiveZeros = Reader.ReadInt32();
                lastWritePositive = Reader.ReadUInt64();
                nextLastWritePositive = Reader.ReadUInt64();
                lastWritePolarity = (PulsePolarity)Reader.ReadInt32();
                highSpeedWriteEvidence = Reader.ReadInt32();
                skippedLast = Reader.ReadBoolean();
                if (Reader.ReadBoolean())
                {
                    transition = transition ?? new Transition(Speed, clock, Read);
                    ok &= transition.Deserialize(Reader, SerializationVersion);
                }
                else
                {
                    transition = null;
                }
                if (Reader.ReadBoolean())
                {
                    readPulseReq = readPulseReq ?? new PulseReq();
                    ok = readPulseReq.Deserialize(Reader, Update, SerializationVersion);
                    if (ok && readPulseReq.Active)
                        computer.AddPulseReq(readPulseReq);
                }
                else
                {
                    readPulseReq = null;
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }
    }
}
