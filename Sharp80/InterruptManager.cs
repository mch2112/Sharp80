/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal sealed class InterruptManager : ISerializable
    {
        private Computer computer;
        private PortSet ports;
        private Tape tape;

        private Trigger rtcIntLatch;
        private Trigger fdcNmiLatch;
        private Trigger fdcMotorOffNmiLatch;
        private Trigger resetButtonLatch;
        private Trigger casMotorOnLatch;
        private Trigger casRisingEdgeIntLatch;
        private Trigger casFallingEdgeIntLatch;
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

            rtcIntLatch = new Trigger(null,
                                      null,
                                      TriggerLock: true,
                                      CanLatchBeforeEnabled: true);

            fdcNmiLatch = new Trigger(null, null, TriggerLock: false, CanLatchBeforeEnabled: true);
            fdcMotorOffNmiLatch = new Trigger(null, null, TriggerLock: false, CanLatchBeforeEnabled: true);

            resetButtonLatch = new Trigger(
                                () => { computer.RegisterPulseReq(new PulseReq(PulseReq.DelayBasis.Microseconds, 200000, () => { resetButtonLatch.Unlatch(); }, false)); },
                                null,
                                TriggerLock: true,
                                CanLatchBeforeEnabled: false)
            {
                Enabled = true
            };

            ioIntLatch = new Trigger(null, null) { Enabled = true };
            extIoIntLatch = new Trigger(null, null) { Enabled = true };
            vidWaitLatch = new Trigger(null, null) { Enabled = true };

            casMotorOnLatch = new Trigger(() => { computer.TapeMotorOnSignal = true; },
                                          () => { computer.TapeMotorOnSignal = false; })
            {
                Enabled = true
            };
            //casRisingEdgeIntLatch = new Trigger(() => { casFallingEdgeIntLatch.Unlatch(); }, null, false, true)
            casRisingEdgeIntLatch = new Trigger(null, null, false, false)
            {
                Enabled = false
            };
            //casFallingEdgeIntLatch = new Trigger(()=> { casRisingEdgeIntLatch.Unlatch(); }, null, false, true)
            casFallingEdgeIntLatch = new Trigger(null, null, false, false)
            {
                Enabled = false
            };

            vidAltCharLatch = new Trigger(() => { computer.SetVideoMode(null, true);},
                                          () => { computer.SetVideoMode(null, false); })
            {
                Enabled = true
            };
            vidWideCharLatch = new Trigger(() => { computer.SetVideoMode(true, null); },
                                          () => { computer.SetVideoMode(false, null); })
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

        public Trigger RtcIntLatch { get { return rtcIntLatch; } }
        public Trigger FdcNmiLatch { get { return fdcNmiLatch; } }
        public Trigger FdcMotorOffNmiLatch { get { return fdcMotorOffNmiLatch; } }
        public Trigger ResetButtonLatch { get { return resetButtonLatch; } }
        public Trigger CassetteRisingEdgeLatch {  get { return casRisingEdgeIntLatch; } }
        public Trigger CassetteFallingEdgeLatch {  get { return casFallingEdgeIntLatch; } }

        public bool NmiTriggered
        {
            get
            {
                return fdcNmiLatch.Triggered || fdcMotorOffNmiLatch.Triggered || resetButtonLatch.Triggered;
            }
        }
        public void ResetNmiTriggers()
        {
            fdcNmiLatch.ResetTrigger();
            fdcMotorOffNmiLatch.ResetTrigger();
            resetButtonLatch.ResetTrigger();
        }
        
        public byte InterruptEnableStatus
        {
            set
            {
                bool oldNmiEnabled = fdcNmiLatch.Enabled;
                bool oldMotorOrDrqNmiEnabled = fdcMotorOffNmiLatch.Enabled;

                fdcNmiLatch.Enabled = value.IsBitSet(7);
                fdcMotorOffNmiLatch.Enabled = value.IsBitSet(6);

                Log.LogDebug(string.Format("FDC NMI Enable: {0} -> {1}", oldNmiEnabled, fdcNmiLatch.Enabled));
                Log.LogDebug(string.Format("Motor / DRQ NMI Enable: {0} -> {1}", oldMotorOrDrqNmiEnabled, fdcMotorOffNmiLatch.Enabled));
            }
        }
        public bool InterruptReq
        {
            get
            {
                return rtcIntLatch.Triggered ||
                       casFallingEdgeIntLatch.Triggered ||
                       casRisingEdgeIntLatch.Triggered;
            }
        }

        // PORT IO

        public byte E0in()
        {
            // reset bit indicates interrupt is in progress [opposite of Model I behavior]

            byte retVal = 0x00;

            // bit 7 is not used
            if (!rs232ErrorIntLatch.Latched)     retVal |= 0x40;
            if (!rs232ReceiveIntLatch.Latched)   retVal |= 0x20;
            if (!rs232XmitIntLatch.Latched)      retVal |= 0x10;
            if (!ioIntLatch.Latched)             retVal |= 0x08;
            if (!rtcIntLatch.Latched)            retVal |= 0x04;
            if (!casFallingEdgeIntLatch.Latched) retVal |= 0x02;
            if (!casRisingEdgeIntLatch.Latched)  retVal |= 0x01;

            Log.LogDebug(string.Format("Read port 0xE0: RTC Interrupt {0}in progress", rtcIntLatch.Latched ? string.Empty : "not "));

            return retVal;
        }
        public byte E4in()
        {
            byte result = 0x00;

            // Set bit if *not* interrupted
            if (!fdcNmiLatch.Latched)
                result |= 0x80;

            if (!fdcMotorOffNmiLatch.Latched)
                result |= 0x40;

            if (!resetButtonLatch.Latched)
                result |= 0x20;

            return result;
        }
        public void ECin()
        {
            if (rtcIntLatch.Latched)
                Log.LogDebug("RTC Interrupt clear (in from port 0xEC)");

            rtcIntLatch.Unlatch();
        }
        public byte FFin()
        {
            byte ret = 0;

            if (casMotorOnLatch.Latched)
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

            casRisingEdgeIntLatch.Unlatch();
            casFallingEdgeIntLatch.Unlatch();

            return ret;
        }
        
        public void E0out(byte b)
        {
            rs232ErrorIntLatch.Enabled =     b.IsBitSet(6);
            rs232ReceiveIntLatch.Enabled =   b.IsBitSet(5);
            rs232XmitIntLatch.Enabled =      b.IsBitSet(4);
            ioIntLatch.Enabled =             b.IsBitSet(3);
            rtcIntLatch.Enabled =            b.IsBitSet(2);
            casFallingEdgeIntLatch.Enabled = b.IsBitSet(1);
            casRisingEdgeIntLatch.Enabled =  b.IsBitSet(0);

            Log.LogDebug(rtcIntLatch.Enabled ? "Enabled RTC Interrupts" : "Disabled RTC Interrupts");
        }
        public void ECout(byte b)
        {
            casMotorOnLatch.LatchIf(b.IsBitSet(1));
            vidWideCharLatch.LatchIf(b.IsBitSet(2));
            vidAltCharLatch.LatchIf(!b.IsBitSet(3)); // seems to be an error in the Tech Ref manual that says 1=enable alt set
            extIoIntLatch.LatchIf(b.IsBitSet(4));
        }
        public void FFout(byte b)
        {
            tape.HandleCasPort(b);
        }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            rtcIntLatch.Serialize(Writer);
            fdcNmiLatch.Serialize(Writer);
            fdcMotorOffNmiLatch.Serialize(Writer);
            resetButtonLatch.Serialize(Writer);
            ioIntLatch.Serialize(Writer);
            casMotorOnLatch.Serialize(Writer);
            casRisingEdgeIntLatch.Serialize(Writer);
            casFallingEdgeIntLatch.Serialize(Writer);
            rs232ErrorIntLatch.Serialize(Writer);
            rs232ReceiveIntLatch.Serialize(Writer);
            rs232XmitIntLatch.Serialize(Writer);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            rtcIntLatch.Deserialize(Reader);
            fdcNmiLatch.Deserialize(Reader);
            fdcMotorOffNmiLatch.Deserialize(Reader);
            resetButtonLatch.Deserialize(Reader);
            ioIntLatch.Deserialize(Reader);
            casMotorOnLatch.Deserialize(Reader);
            casRisingEdgeIntLatch.Deserialize(Reader);
            casFallingEdgeIntLatch.Deserialize(Reader);
            rs232ErrorIntLatch.Deserialize(Reader);
            rs232ReceiveIntLatch.Deserialize(Reader);
            rs232XmitIntLatch.Deserialize(Reader);
        }
    }
}       