/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Sharp80;

namespace Sharp80.TRS80
{
    public class CmdFile
    {
        public string Title { get; private set; } = "UNTITLED";
        public ushort? ExecAddress { get; private set; } = null;
        public bool Valid { get; private set; } = false;
        public string FilePath { get; private set; }
        public int Size { get; private set; }
        public ushort LowAddress { get; private set; }
        public ushort HighAddress { get; private set; }
        public bool IsLoaded { get; private set; }

        public int NumSegments => segments.Count;

        private List<(ushort SegmentAddress, byte[] Bytes)> segments = new List<(ushort SegmentAddress, byte[] Bytes)>();

        /// <summary>
        /// For a CMD file that already exists
        /// </summary>
        public CmdFile(string Path)
        {
            FilePath = Path;
            Load();
        }

        /// <summary>
        /// For a raw assembly; will write the CMD File
        /// </summary>
        /// <param name="Assembly"></param>
        /// <param name="Path"></param>
        public CmdFile(Processor.Assembler.Assembly Assembly, string Path)
        {
            FilePath = Path;
            try
            {
                Title = Title ?? MakeTitle(System.IO.Path.GetFileNameWithoutExtension(FilePath));

                var writer = new BinaryWriter(File.Open(FilePath, FileMode.Create));

                ushort dest;
                int cursor;
                byte lowDest;
                byte highDest;
                int segmentSize;
                int blockSize;

                writer.Write((byte)0x05);
                writer.Write((byte)Title.Length);
                for (int i = 0; i < Title.Length; i++)
                    writer.Write((byte)Title[i]);

                foreach (var d in Assembly.Segments)
                {
                    dest = d.SegmentAddress;
                    cursor = 0;

                    segmentSize = d.Bytes.Length;

                    while (cursor < segmentSize)
                    {
                        blockSize = Math.Min(0x100, d.Bytes.Length - cursor);
                        writer.Write((byte)0x01);   // block marker
                        writer.Write((byte)(blockSize + 2)); // 0x02 == 256 bytes
                        dest.Split(out lowDest, out highDest);
                        writer.Write(lowDest);
                        writer.Write(highDest);
                        while (blockSize-- > 0)
                        {
                            writer.Write(d.Bytes[cursor++]);
                            dest++;
                        }
                    }
                }
                writer.Write((byte)0x02);  // transfer address marker
                writer.Write((byte)0x02);  // transfer address length
                Assembly.ExecAddress.Split(out lowDest, out highDest);
                writer.Write(lowDest);
                writer.Write(highDest);

                writer.Close();
                Load();
            }
            catch
            {
                Valid = false;
            }
        }

        internal bool Load(IMemory Memory)
        {
            if (Valid)
            {
                foreach (var b in segments)
                    for (int i = 0; i < b.Bytes.Length; i++)
                        Memory[(ushort)(i + b.SegmentAddress)] = b.Bytes[i];
                IsLoaded = true;
                return true;
            }
            else
            {
                IsLoaded = false;
                return false;
            }
        }

        public IEnumerable<(ushort Address, IList<byte> Bytes)> Segments => segments.Select(s => (s.SegmentAddress, (IList<byte>)s.Bytes));

        private void Load()
        {
            // http://www.tim-mann.org/trs80/doc/ldosq1-4.txt has some
            // good information on the CMD file format

            byte code;
            int length;

            try
            {
                if (IO.LoadBinaryFile(FilePath, out byte[] b))
                {
                    int i = 0;
                    while (i < b.Length - 2)
                    {
                        code = b[i++];
                        length = b[i++];

                        length = Math.Min(length, b.Length - i);
                        if (length == 0)
                            length = 0x100;

                        switch (code)
                        {
                            case 0x00:
                                // do nothing
                                break;
                            case 0x01:          // object code (load block)
                                var addr = Lib.CombineBytes(b[i++], b[i++]);
                                if (length < 0x03)
                                    length += 0xFE;
                                else
                                    length -= 0x02;
                                var data = new byte[length];
                                Array.Copy(b, i, data, 0, length);
                                segments.Add((addr, data));
                                break;
                            case 0x02:          // transfer address
                                if (length == 0x01)
                                    ExecAddress = b[i++];
                                else if (length == 0x02)
                                    ExecAddress = Lib.CombineBytes(b[i++], b[i++]);
                                else
                                    throw new Exception("CMD file Error.");
                                Finalize(true);
                                return;
                            case 0x03:
                                // non executable marker
                                Finalize(true);
                                return;
                            case 0x04: break;   // end of partitioned data set member
                            case 0x05:
                                Title = String.Empty;
                                for (int j = 0; j < length; j++)
                                    Title += (char)b[i + j];
                                break;
                            case 0x06: break;   // partitioned data set header
                            case 0x07: break;   // patch name header
                            case 0x08: break;   // ISAM directory entry
                            case 0x09: break;   // unused code
                            case 0x0A: break;   // end of ISAM directory
                            case 0x0C: break;   // PDS directory entry
                            case 0x0E: break;   // end of PDS directory
                            case 0x10: break;   // yanked load block
                            case 0x1F: break;   // copyright block
                        }
                        i += length;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, "Error loading CMD File " + FilePath);
            }
            Finalize(false);
        }
        private void Finalize(bool Valid)
        {
            this.Valid = Valid && segments.Count > 0;
            if (Valid)
            {
                segments = segments.OrderBy(b => b.SegmentAddress).ToList();
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    // are these adjoining blocks?
                    if (segments[i].SegmentAddress + segments[i].Bytes.Length == segments[i + 1].SegmentAddress)
                    {
                        // if so, combine them
                        byte[] data = new byte[segments[i].Bytes.Length + segments[i + 1].Bytes.Length];
                        Array.Copy(segments[i].Bytes, 0, data, 0, segments[i].Bytes.Length);
                        Array.Copy(segments[i + 1].Bytes, 0, data, segments[i].Bytes.Length, segments[i + 1].Bytes.Length);
                        segments[i] = (segments[i].SegmentAddress, data);
                        segments.RemoveAt(i + 1);
                        i--;
                    }
                }
                Size = segments.Sum(b => b.Bytes.Length);
                Valid &= Size > 0;
                LowAddress = segments[0].SegmentAddress;
                HighAddress = (ushort)(segments.Last().SegmentAddress + segments.Last().Bytes.Length - 1);
            }
            else
            {
                Size = 0;
                LowAddress = 0;
                HighAddress = 0;
            }
        }
        private static string MakeTitle(string FileName)
        {
            FileName = FileName.ToUpper();
            return String.Join(String.Empty, FileName.Where(c => c.IsBetween('A', 'Z')).Take(Processor.Assembler.Assembler.MAX_TITLE_LENGTH));
        }
    }
}
