/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

//#define NOROM

using System;
using System.Diagnostics;
using System.IO;

namespace Sharp80
{
    internal sealed partial class Memory : IMemory
    {
        public const ushort VIDEO_MEMORY_BLOCK = 0x3C00;

        private const ushort KEYBOARD_MEMORY_BLOCK = 0x3800;

        private byte[] mem;            // The entire memory space of the TRS80
        public ushort firstRAMByte;    // 1 + the last ROM byte
        
        public bool ScreenWritten { get; set; }     // True if the screen needs to be updated (i.e. write to memory location 0x3C00 to 0x3FFF)
                                                    // This can be set to true to force a screen refresh
        public Memory()
        {
            mem = new byte[0x10000];

#if NOROM
            firstRAMByte = 0;
#else
            LoadRom();
#endif
            for (int i = firstRAMByte; i < mem.GetUpperBound(0); i++)
                mem[i] = 0x00;

            SetupDXKeyboardMatrix();
        }

        // RAM ACCESS

        public byte this[ushort Location]
        {
            get
            {
                if (Location >= 0x37e0 && Location <= 0x37ff)
                    Debug.WriteLine("Model 1 FDC attempt?");

                unchecked
                {
                    if ((Location & 0xFF00) == KEYBOARD_MEMORY_BLOCK)  // Keyboard Memory Map
                    {
                        byte ret = 0;
                        if ((Location & 0x01) == 0x01)
                            ret |= mem[0x3801];
                        if ((Location & 0x02) == 0x02)
                            ret |= mem[0x3802];
                        if ((Location & 0x04) == 0x04)
                            ret |= mem[0x3804];
                        if ((Location & 0x08) == 0x08)
                            ret |= mem[0x3808];
                        if ((Location & 0x10) == 0x10)
                            ret |= mem[0x3810];
                        if ((Location & 0x20) == 0x20)
                            ret |= mem[0x3820];
                        if ((Location & 0x40) == 0x40)
                            ret |= mem[0x3840];
                        if ((Location & 0x80) == 0x80)
                            ret |= mem[0x3880];

                        return ret;
                    }
                    else
                    {
                        return mem[Location];
                    }
                }
            }
            set
            {
                unchecked
                {
                    if (Location >= firstRAMByte)
                    {
                        if ((Location & 0xFC00) == VIDEO_MEMORY_BLOCK)
                            ScreenWritten |= (mem[Location] != value);
                        mem[Location] = value;
                    }
                    else
                    {
                        Log.LogDebug(string.Format("Write attempt {0:X2} to ROM Locdtion {1:X4}", Location, value));
                    }
                }
            }
        }

        public void SetWordAt(ushort Location, ushort Value)
        {
            unchecked
            {
                Value.Split(out byte lowByte, out byte highByte);

                this[Location++] = lowByte;
                this[Location] = highByte;
            }
        }
        public ushort GetWordAt(ushort Location)
        {
            unchecked
            {
                return Lib.CombineBytes(this[Location++],
                                        this[Location]);
            }
        }

        // SNAPSHOTS

        public void Serialize(BinaryWriter Writer)
        {
            Writer.Write(mem);
            Writer.Write(firstRAMByte);
            Writer.Write(ScreenWritten);
        }
        public void Deserialize(BinaryReader Reader)
        {
            Array.Copy(Reader.ReadBytes(0x10000), mem, 0x10000);
            firstRAMByte = Reader.ReadUInt16();
            ScreenWritten = Reader.ReadBoolean();
        }
        private void LoadRom()
        {
            byte[] b = Resources.ModelIIIRom;
            Array.Copy(b, mem, b.Length);

            // TODO: Validate the rom should have this:
            mem[14312] = mem[14313] = 63;

            firstRAMByte = 0x3C00;
        }
    }
}
