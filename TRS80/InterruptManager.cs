/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    internal class InterruptManager : ISerializable
    {
        private Computer computer;
        private PortSet ports;
        private Tape tape;

        public Trigger RtcIntLatch { get; private set; }
        public Trigger FdcNmiLatch { get; private set; }
        public Trigger FdcMotorOffNmiLatch { get; private set; }
        public Trigger ResetButtonLatch { get; private set; }
        public Trigger CasMotorOnLatch { get; private set; }
        public Trigger CasRisingEdgeIntLatch { get; private set; }
        public Trigger CasFallingEdgeIntLatch { get; private set; }

        private Trigger vidAltCharLatch;
        private Trigger vidWideCharLatch;

        private Trigger vidWaitLatch;
        private Trigger extIoIntLatch;
        private Trigger ioIntLatch;
        private Trigger rs232ErrorIntLatch;
        private Trigger rs232ReceiveIntLatch;
        private Trigger rs232XmitIntLatch;

        public InterruptManager(Computer Computer)
        {
            computer = Computer;

            RtcIntLatch = new Trigger(null,
                                      null,
                                      TriggerLock: true,
                                      CanLatchBeforeEnabled: true);

            FdcNmiLatch = new Trigger(null, null, TriggerLock: false, CanLatchBeforeEnabled: true);
            FdcMotorOffNmiLatch = new Trigger(null, null, TriggerLock: false, CanLatchBeforeEnabled: true);

            ResetButtonLatch = new Trigger(
                                () => { computer.Activate(new PulseReq(PulseReq.DelayBasis.Microseconds, 200000, () => { ResetButtonLatch.Unlatch(); }, false)); },
                                null,
                                TriggerLock: true,
                                CanLatchBeforeEnabled: false)
            {
                Enabled = true
            };

            ioIntLatch = new Trigger(null, null) { Enabled = true };
            extIoIntLatch = new Trigger(null, null) { Enabled = true };
            vidWaitLatch = new Trigger(null, null) { Enabled = true };

            CasMotorOnLatch = new Trigger(() => { computer.TapeMotorOnSignal = true; },
                                          () => { computer.TapeMotorOnSignal = false; })
            {
                Enabled = true
            };
            CasRisingEdgeIntLatch = new Trigger(null, null, false, false)
            {
                Enabled = false
            };
            CasFallingEdgeIntLatch = new Trigger(null, null, false, false)
            {
                Enabled = false
            };

            vidAltCharLatch = new Trigger(() => { computer.AltCharMode = true; },
                                          () => { computer.AltCharMode = false; })
            {
                Enabled = true
            };
            vidWideCharLatch = new Trigger(() => { computer.WideCharMode = true; },
                                           () => { computer.WideCharMode = false; })
            {
                Enabled = true
            };

            rs232ErrorIntLatch = new Trigger(null, null);
            rs232ReceiveIntLatch = new Trigger(null, null);
            rs232XmitIntLatch = new Trigger(null, null);
        }

        public void Initialize(PortSet Ports, Tape Tape)
        {
            ports = Ports;
            tape = Tape;
        }

        public void ResetNmiTriggers()
        {
            FdcNmiLatch.ResetTrigger();
            FdcMotorOffNmiLatch.ResetTrigger();
            ResetButtonLatch.ResetTrigger();
        }

        public bool Nmi => FdcNmiLatch.Triggered || FdcMotorOffNmiLatch.Triggered || ResetButtonLatch.Triggered;
        public bool Irq => RtcIntLatch.Triggered || CasFallingEdgeIntLatch.Triggered || CasRisingEdgeIntLatch.Triggered;
        public byte InterruptEnableStatus
        {
            set
            {
                bool oldNmiEnabled = FdcNmiLatch.Enabled;
                bool oldMotorOrDrqNmiEnabled = FdcMotorOffNmiLatch.Enabled;

                FdcNmiLatch.Enabled = value.IsBitSet(7);
                FdcMotorOffNmiLatch.Enabled = value.IsBitSet(6);
            }
        }

        // PORT IO

        public byte E0in()
        {
            // reset bit indicates interrupt is in progress [opposite of Model I behavior]

            byte retVal = 0x00;

            // bit 7 is not used
            if (!rs232ErrorIntLatch.Latched) retVal |= 0x40;
            if (!rs232ReceiveIntLatch.Latched) retVal |= 0x20;
            if (!rs232XmitIntLatch.Latched) retVal |= 0x10;
            if (!ioIntLatch.Latched) retVal |= 0x08;
            if (!RtcIntLatch.Latched) retVal |= 0x04;
            if (!CasFallingEdgeIntLatch.Latched) retVal |= 0x02;
            if (!CasRisingEdgeIntLatch.Latched) retVal |= 0x01;

            return retVal;
        }
        public byte E4in()
        {
            byte result = 0x00;

            // Set bit if *not* interrupted
            if (!FdcNmiLatch.Latched)
                result |= 0x80;

            if (!FdcMotorOffNmiLatch.Latched)
                result |= 0x40;

            if (!ResetButtonLatch.Latched)
                result |= 0x20;

            return result;
        }
        public void ECin() => RtcIntLatch.Unlatch();
        
        public byte FFin()
        {
            byte ret = 0;

            if (CasMotorOnLatch.Latched)
                ret |= 0x02;
            if (vidWideCharLatch.Latched)
                ret |= 0x04;
            if (vidAltCharLatch.Latched)
                ret |= 0x08;
            if (extIoIntLatch.Latched)
                ret |= 0x10;
            if (vidWaitLatch.Latched)
                ret |= 0x20;

            ret |= tape.Value;

            CasRisingEdgeIntLatch.Unlatch();
            CasFallingEdgeIntLatch.Unlatch();

            return ret;
        }

        public void E0out(byte b)
        {
            rs232ErrorIntLatch.Enabled = b.IsBitSet(6);
            rs232ReceiveIntLatch.Enabled = b.IsBitSet(5);
            rs232XmitIntLatch.Enabled = b.IsBitSet(4);
            ioIntLatch.Enabled = b.IsBitSet(3);
            RtcIntLatch.Enabled = b.IsBitSet(2);
            CasFallingEdgeIntLatch.Enabled = b.IsBitSet(1);
            CasRisingEdgeIntLatch.Enabled = b.IsBitSet(0);
        }
        public void ECout(byte b)
        {
            CasMotorOnLatch.LatchIf(b.IsBitSet(1));
            vidWideCharLatch.LatchIf(b.IsBitSet(2));
            vidAltCharLatch.LatchIf(!b.IsBitSet(3)); // seems to be an error in the Tech Ref manual that says 1=enable alt set
            extIoIntLatch.LatchIf(b.IsBitSet(4));
            vidWaitLatch.LatchIf(b.IsBitSet(5));
        }
        public void FFout(byte b)
        {
            tape.WriteToCasPort(b);
        }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            RtcIntLatch.Serialize(Writer);
            FdcNmiLatch.Serialize(Writer);
            FdcMotorOffNmiLatch.Serialize(Writer);
            ResetButtonLatch.Serialize(Writer);
            ioIntLatch.Serialize(Writer);
            CasMotorOnLatch.Serialize(Writer);
            CasRisingEdgeIntLatch.Serialize(Writer);
            CasFallingEdgeIntLatch.Serialize(Writer);
            rs232ErrorIntLatch.Serialize(Writer);
            rs232ReceiveIntLatch.Serialize(Writer);
            rs232XmitIntLatch.Serialize(Writer);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                return
                    RtcIntLatch.Deserialize(Reader, DeserializationVersion) &&
                    FdcNmiLatch.Deserialize(Reader, DeserializationVersion) &&
                    FdcMotorOffNmiLatch.Deserialize(Reader, DeserializationVersion) &&
                    ResetButtonLatch.Deserialize(Reader, DeserializationVersion) &&
                    ioIntLatch.Deserialize(Reader, DeserializationVersion) &&
                    CasMotorOnLatch.Deserialize(Reader, DeserializationVersion) &&
                    CasRisingEdgeIntLatch.Deserialize(Reader, DeserializationVersion) &&
                    CasFallingEdgeIntLatch.Deserialize(Reader, DeserializationVersion) &&
                    rs232ErrorIntLatch.Deserialize(Reader, DeserializationVersion) &&
                    rs232ReceiveIntLatch.Deserialize(Reader, DeserializationVersion) &&
                    rs232XmitIntLatch.Deserialize(Reader, DeserializationVersion);
            }
            catch
            {
                return false;
            }
        }
    }
}