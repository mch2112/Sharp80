/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal sealed class PortSet
    {
        private FloppyController floppyController;

        private const int NUM_PORTS = 0x100;
        private Computer computer;
        private Tape tape;
        private Printer printer;
        private InterruptManager intMgr;
        
        private byte[] ports = new byte[NUM_PORTS];
        private byte lastFFout = 0;

        public PortSet(Computer Computer)
        {
            computer = Computer;
        }
        public void Initialize(FloppyController FloppyController, InterruptManager InterruptManager, Tape Tape, Printer Printer)
        {
            floppyController = FloppyController;
            intMgr = InterruptManager;
            tape = Tape;
            printer = Printer;
            Reset();
        }
        public void Reset()
        {
            lastFFout = 0;

            for (int i = 0; i < NUM_PORTS; i++)
                ports[i] = 0xFF;

            ports[0x50] = 0x00;
            ports[0x51] = 0x00;
            ports[0x52] = 0x00;
            ports[0x53] = 0x00;
            ports[0x54] = 0x00;
            ports[0x55] = 0x00;
            ports[0x56] = 0x00;
            ports[0x57] = 0x00;
            ports[0x58] = 0x00;
            ports[0x59] = 0x00;
            ports[0x5A] = 0x00;
            ports[0x5B] = 0x00;
            ports[0x5C] = 0x00;
            ports[0x5D] = 0x00;
            ports[0x5E] = 0x00;
            ports[0x5F] = 0x32;
            ports[0x68] = 0x59;
            ports[0x69] = 0x1E;
            ports[0x6A] = 0x16;
            ports[0x6B] = 0x05;
            ports[0x6C] = 0x24;
            ports[0x6D] = 0x04; 
            ports[0x71] = 0x00;
            ports[0xE0] = 0xFB;
            ports[0xE4] = 0x00;     // FDC Status
            ports[0xEC] = 0x12;     // Reset clock / Various controls
            ports[0xF0] = 0x80;                
            ports[0xF1] = 0x00;            
            ports[0xF9] = 0x3F;
            ports[0xFA] = 0x3F;
            ports[0xFB] = 0x3F;
            ports[0xFC] = 0x89;
            ports[0xFD] = 0x89;
            ports[0xFE] = 0x89;
            ports[0xFF] = 0x89;     // cassette, not ready
        }

        public byte this[byte PortNumber]
        {
            get
            {
                switch (PortNumber)
                {
                    case 0xE0:
                    case 0xE1:
                    case 0xE2:
                    case 0xE3:
                        ports[PortNumber] = intMgr.E0in();
                        break;
                    case 0xEC:
                    case 0xED:
                    case 0xEE:
                    case 0xEF:
                        ports[0xEC] = ports[0xED] = ports[0xEE] = ports[0xEF] = 0xFF;
                        intMgr.ECin();
                        break;
                    case 0xE4:
                        ports[0xE4] = intMgr.E4in();
                        break;
                    case 0xF0:
                    case 0xF1:
                    case 0xF2:
                    case 0xF3:
                        floppyController.FdcIoEvent(PortNumber, 0, false);
                        break;
                    case 0xF8:
                        ports[0xF8] = printer.PrinterStatus;
                        break;
                    case 0xFF:
                        ports[0xFF] = intMgr.FFin();
                        break;
                }
                Log.LogDebug("Reading Port " + PortNumber.ToHexString() + " [" + ports[PortNumber].ToHexString() + "]");
                return ports[PortNumber];
            }
            set
            {
                switch (PortNumber)
                {
                    case 0xE0:
                    case 0xE1:
                    case 0xE2:
                    case 0xE3:
                        intMgr.E0out(value);
                        break;
                    case 0xEC:
                    case 0xED:
                    case 0xEE:
                    case 0xEF:
                        // Update double width and Kanji character settings
                        intMgr.ECout(value);
                        break;
                    case 0xE4:
                    case 0xE5:
                    case 0xE6:
                    case 0xE7:
                    case 0xF0:
                    case 0xF1:
                    case 0xF2:
                    case 0xF3:
                    case 0xF4:
                        floppyController.FdcIoEvent(PortNumber, value, true);
                        break;
                    case 0xF8:
                        printer.Print(value);
                        break;
                    case 0xFF:
                        lastFFout = value;
                        intMgr.FFout(value);
                        break;
                    default:
                        break;
                }
                Log.LogDebug("Writing Port " + PortNumber.ToHexString() + " [" + value.ToHexString() + "]");
                return;
            }
        }
        /// <summary>
        /// Used to avoid side effects
        /// </summary>
        public void SetPortDirect(byte PortNum, byte B) => ports[PortNum] = B;
        
        /// <summary>
        /// Used to avoid side effects
        /// </summary>
        public byte GetPortDirect(byte PortNum) => ports[PortNum];
        
        public byte CassetteOut() => lastFFout;
      
        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(ports);
            Writer.Write(lastFFout);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                Array.Copy(Reader.ReadBytes(NUM_PORTS), ports, NUM_PORTS);
                lastFFout = Reader.ReadByte();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
