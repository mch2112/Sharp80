﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

//#define NOROM

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sharp80.TRS80
{
    internal partial class Memory : IMemory, Z80.IMemory
    {
        public const ushort VIDEO_MEMORY_BLOCK = 0x3C00;

        private const ushort KEYBOARD_MEMORY_BLOCK = 0x3800;

        public ArraySegment<byte> VideoMemory { get; private set; }

        private readonly byte[] mem;            // The entire memory space of the TRS80
        private ushort firstRAMByte;    // 1 + the last ROM byte

        public Memory()
        {
            mem = new byte[Z80.Z80.MEMORY_SIZE];
            VideoMemory = new ArraySegment<byte>(mem, 0x3C00, 0x400);

#if NOROM
            firstRAMByte = 0;
#else
            LoadRom();
#endif
            // printer status, same as port 0xF8 in
            // some emulators map writes to this address to the printer port,
            // but this isn't reflected in the technical docuentaion so 
            // we ignore writes to this (and all addresses below 0x3c00)
            mem[0x37E8] = 0x30;

            SetupKeyboardMatrix();
        }

        // RAM ACCESS

        public int Count => mem.Length;
        public byte this[int Location] => this[(ushort)Location];
        public IEnumerator<byte> GetEnumerator()
        {
            foreach (byte b in mem)
                yield return b;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public byte this[ushort Location]
        {
            get
            {
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
                        mem[Location] = value;
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
                return Lib.CombineBytes(this[Location++], this[Location]);
            }
        }
        public IReadOnlyCollection<byte> ToArray()
        {
            return Array.AsReadOnly(mem);
        }

        // SNAPSHOTS

        public void Serialize(BinaryWriter Writer)
        {
            Writer.Write(mem);
            Writer.Write(firstRAMByte);
        }
        public bool Deserialize(BinaryReader Reader, int DeserializationVersion)
        {
            try
            {
                Array.Copy(Reader.ReadBytes(Z80.Z80.MEMORY_SIZE), mem, Z80.Z80.MEMORY_SIZE);
                firstRAMByte = Reader.ReadUInt16();
                return true;
            }
            catch
            {
                return false;
            }
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
