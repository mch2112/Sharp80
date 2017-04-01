/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Sharp80.Z80
{
    public class CircularBuffer : IEnumerable<ushort>, ISerializable
    {
        private int writeCursor;
        private ushort[] contents;
        public CircularBuffer(int Size)
        {
            if (Size < 1)
                throw new Exception("Zero Circular Buffer Size");
            this.Size = Size;
            Clear();
        }

        public int Size { get; private set; }
        public int Count { get; private set; }
        public bool Full { get; private set; }

        public void Add(ushort Item)
        {
            if (writeCursor >= Size - 1)
                writeCursor = 0;
            else
                writeCursor++;

            contents[writeCursor] = Item;
            if (!Full)
            {
                Full = writeCursor >= Size - 1;
                Count++;
            }
        }
        public void ReplaceLast(ushort Item)
        {
            contents[writeCursor] = Item;
        }
        public void Clear()
        {
            contents = new ushort[Size];
            Count = 0;
            Full = false;
            writeCursor = -1;
        }
        /// <summary>
        /// Returns all elements, oldest to newest
        /// </summary>
        public IEnumerator<ushort> GetEnumerator()
        {
            int start, end;

            if (Full)
            {
                start = (writeCursor + 1) % Size;
                end = start + Size - 1;
                int i = start;
                while (i <= end)
                    yield return contents[(i++ % Size)];
            }
            else
            {
                for (int i = 0; i <= writeCursor; i++)
                    yield return contents[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(writeCursor);
            Writer.Write(Count);
            Writer.Write(Size);
            for (int i = 0; i < Size; i++)
                Writer.Write(contents[i]);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int SerializationVersion)
        {
            writeCursor = Reader.ReadInt32();
            Count = Reader.ReadInt32();
            Size = Reader.ReadInt32();

            // Hack to avoid breaking serialization of older versions:
            if (SerializationVersion < 9)
                Size = Count = 22;

            for (int i = 0; i < Size; i++)
                contents[i] = Reader.ReadUInt16();

            return true;
        }
    }
}
