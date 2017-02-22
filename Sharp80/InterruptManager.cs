using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp80
{
    internal sealed class InterruptManager : ISerializable
    {
        Computer computer;

        // Used

        private Trigger rtcIntLatch;
        private Trigger fdcNmiLatch;
        private Trigger fdcMotorOffNmiLatch;
        private Trigger resetButtonLatch;

        // Unused

        private Trigger ioIntLatch;
        private Trigger casRisingEdgeIntLatch;
        private Trigger casFallingEdgeIntLatch;
        private Trigger rs232ErrorIntLatch;
        private Trigger rs232ReceiveIntLatch;
        private Trigger rs232XmitIntLatch;

        public InterruptManager(Computer Comp)
        {
            this.computer = Comp;

            rtcIntLatch = new Trigger(null,
                                      null,
                                      TriggerLock: true,
                                      CanLatchBeforeEnabled: true);

            fdcNmiLatch         = new Trigger(null, null, TriggerLock: false, CanLatchBeforeEnabled: true);
            fdcMotorOffNmiLatch = new Trigger(null, null, TriggerLock: false, CanLatchBeforeEnabled: true);

            resetButtonLatch = new Trigger(
                () => { computer.Clock.RegisterPulseReq(
                    new PulseReq(200000, () => {
                        resetButtonLatch.Unlatch();
                    }, false)); },
                                              null,
                                              TriggerLock: true,
                                              CanLatchBeforeEnabled: false)
                                   { Enabled = true };

            ioIntLatch = new Trigger(null, null);
            casRisingEdgeIntLatch = new Trigger(null, null);
            casFallingEdgeIntLatch = new Trigger(null, null);
            rs232ErrorIntLatch = new Trigger(null, null);
            rs232ReceiveIntLatch = new Trigger(null, null);
            rs232XmitIntLatch = new Trigger(null, null);
        }

        public Trigger RtcIntLatch { get { return rtcIntLatch; } }
        public Trigger FdcNmiLatch { get { return fdcNmiLatch; } }
        public Trigger FdcMotorOffNmiLatch { get { return fdcMotorOffNmiLatch; } }
        public Trigger ResetButtonLatch { get { return resetButtonLatch; } }

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

        public void E4in()
        {
            byte result = 0x00;

            // Set bit if *not* interrupted
            if (!fdcNmiLatch.Latched)
                result |= 0x80;

            if (!fdcMotorOffNmiLatch.Latched)
                result |= 0x40;

            if (!resetButtonLatch.Latched)
                result |= 0x20;
            else
                System.Diagnostics.Debug.WriteLine("foo");

            computer.Ports.SetPortArray(result, 0xE4);
        }
        public byte InterruptEnableStatus
        {
            set
            {
                bool oldNmiEnabled = fdcNmiLatch.Enabled;
                bool oldMotorOrDrqNmiEnabled = fdcMotorOffNmiLatch.Enabled;

                fdcNmiLatch.Enabled = Lib.IsBitSet(value, 7);
                fdcMotorOffNmiLatch.Enabled = Lib.IsBitSet(value, 6);

                if (Log.DebugOn)
                {
                    Log.LogToDebug(string.Format("FDC NMI Enable: {0} -> {1}", oldNmiEnabled, fdcNmiLatch.Enabled));
                    Log.LogToDebug(string.Format("Motor / DRQ NMI Enable: {0} -> {1}", oldMotorOrDrqNmiEnabled, fdcMotorOffNmiLatch.Enabled));
                }
            }
        }
        public void ECin()
        {
            if (Log.DebugOn )
                if (rtcIntLatch.Latched)
                    Log.LogToDebug("RTC Interrupt clear (in from port 0xEC)");

            rtcIntLatch.Unlatch();

            computer.Ports.SetPortArray((byte)0xFF, (byte)0xEC);
        }
        public void FFin()
        {
            casRisingEdgeIntLatch.Unlatch();
            casFallingEdgeIntLatch.Unlatch();
        }
        public byte WrIntMaskReg
        {
            // uses input and output for port E0 to manage interrupts
            get
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

                if (Log.DebugOn)
                    Log.LogToDebug(string.Format("Read port 0xE0: RTC Interrupt {0}in progress", rtcIntLatch.Latched ? string.Empty : "not "));

                return retVal;
            }
            set
            {
                rs232ErrorIntLatch.Enabled     = Lib.IsBitSet(value, 6);
                rs232ReceiveIntLatch.Enabled   = Lib.IsBitSet(value, 5);
                rs232XmitIntLatch.Enabled      = Lib.IsBitSet(value, 4);
                ioIntLatch.Enabled             = Lib.IsBitSet(value, 3);
                rtcIntLatch.Enabled            = Lib.IsBitSet(value, 2);
                casFallingEdgeIntLatch.Enabled = Lib.IsBitSet(value, 1);
                casRisingEdgeIntLatch.Enabled  = Lib.IsBitSet(value, 0);

                if (Log.DebugOn)
                    Log.LogToDebug(rtcIntLatch.Enabled ? "Enabled RTC Interrupts" : "Disabled RTC Interrupts");
            }
        }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            rtcIntLatch.Serialize(Writer);
            fdcNmiLatch.Serialize(Writer);
            fdcMotorOffNmiLatch.Serialize(Writer);
            resetButtonLatch.Serialize(Writer);
            ioIntLatch.Serialize(Writer);
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
            casRisingEdgeIntLatch.Deserialize(Reader);
            casFallingEdgeIntLatch.Deserialize(Reader);
            rs232ErrorIntLatch.Deserialize(Reader);
            rs232ReceiveIntLatch.Deserialize(Reader);
            rs232XmitIntLatch.Deserialize(Reader);
        }
    }
}       