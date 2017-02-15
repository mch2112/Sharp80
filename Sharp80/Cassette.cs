using System;
using System.Collections.Generic;
using System.IO;

#if CASSETTE

namespace Sharp80
{
    public sealed class Cassette
    {
        public delegate ulong CassetteReadCallback();

        public bool Loaded { get { return contents != null; } }
        
        private InterruptManager interruptManager;
        private Clock clock;
        private bool motorOn = false;
        private ulong pulseLengthInuSec;
        
        public Cassette(Computer Computer)
        {
            this.interruptManager = Computer.IntMgr;
            this.clock = Computer.Clock;
            pulseLengthInuSec = 1000000 / 1500 * 2;
        }

        private byte[] contents = new byte[0];

        private int cursor = 0;
        private ulong tapePosnInuSec = 0;
        public void LoadCassete(string FilePath)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(FilePath, FileMode.Open, FileAccess.Read)))
                {
                    List<byte> bytes = new List<byte>();
                    byte[] b;
                    while ((b = reader.ReadBytes(1000)).Length > 0)
                    {
                        bytes.AddRange(b);
                    }

                    contents = bytes.ToArray();
                }
                Rewind();
            }
            catch (Exception Ex)
            {
                Log.LogToConsole(Ex.ToString());
            }
        }
        private const bool LITTLE_ENDIAN = true;
        public bool Rewind()
        {
            cursor = 0;
            tapePosnInuSec = 0;
            readBitShift = LITTLE_ENDIAN ? 7 : 0;

            return contents.Length > 0;
        }

        private int readBitShift;
        private bool readBit = false;
        private bool oldReadBit = false;
        public byte Read()
        {
            Log.LogToConsole("Cassette Read " + (readBit ? "1" : "0"));

            if (pulsing)
                return oldReadBit ? (byte)0x01 : (byte)0x00;
            else
                return readBit ? (byte)0x01 : (byte)0x00;
        }
        private void read()
        {
            if (MotorOn)
            {
                if (cursor < contents.Length)
                {
                    oldReadBit = readBit;
                    readBit = ((contents[cursor] >> readBitShift) & 0x01) != 0;

                    if (LITTLE_ENDIAN)
                        readBitShift = (readBitShift + 7) % 8; // subtract one
                    else
                        readBitShift = (readBitShift + 1) % 8; // add one

                    if (readBitShift == (LITTLE_ENDIAN ? 7 : 0)) // if we've wrapped around
                    {
                        cursor++;
                    }
                }
                Log.LogToConsole("Cassette advanced one bit, " + (readBit ? "1" : "0") + " in buffer.");
            }
            else
            {
                readBit = false;
                Log.LogToConsole("No cassette read: Motor off");
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
                    if (motorOn)
                        clock.RegisterPulseReq(new PulseReq(500, clock.DoCasIrqNow));
                }
                motorOn = value;

                Log.LogToConsole("Cassette Motor " + (motorOn ? "On" : "Off"));
            }
        }
        //private bool risingEdge = true;

        public CassetteReadCallback CassetteCallback { get { return cassetteCallback; } }

        bool pulsing = false;
        // returns time until next cassette interrupt needed, in usec
        private ulong cassetteCallback()
        {
            if (motorOn)
            {
                if (!pulsing)
                    read();

                pulsing = !pulsing;
                
                if (pulsing)
                    interruptManager.LatchCasRisingEdgeInterrupt();
                else
                    interruptManager.LatchCasFallingEdgeInterrupt();

                ulong time;

                if (cursor >= contents.Length)
                    time = 0;
                else if (readBit)
                    //time = pulseLengthInuSec;
                    time = 188 * 4;
                else
                    //time = pulseLengthInuSec;
                    time = 376 * 4;

                tapePosnInuSec += time;
                
                return time;
            }
            else
            {
                return 0;
            }
        }
        public string GetDriveStatusReport()
        {
            //if (motorOn)
            if (contents.Length > 0)
                return (motorOn ? "[*" : "[ ") + TimeSpan.FromSeconds(tapePosnInuSec / 1000000).ToString(@"mm\:ss") + "]";
                //if (cursor < contents.Length)
                //    return "O_O";
                //else
                //    return "X_X";
            else
                return "XX:XX";
        }
    }
}


#endif