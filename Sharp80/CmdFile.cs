using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class CmdFile
    {
        public const int MAX_TITLE_LENGTH = 6;

        public string Title { get; private set; } = "UNTITLED";
        public ushort? ExecAddress { get; private set; } = null;
        public bool Valid { get; private set; } = false;
        public string FilePath { get; private set; }
        public int Size { get; private set; }
        public int NumBlocks {  get { return segments.Count; } }
        public ushort LowAddress { get; private set; }
        public ushort HighAddress { get; private set; }
        public bool IsLoaded { get; private set; }

        private List<(ushort SegmentAddress, byte[] Bytes)> segments = new List<(ushort SegmentAddress, byte[] Bytes)>();

        public CmdFile(string Path)
        {
            // http://www.tim-mann.org/trs80/doc/ldosq1-4.txt has some
            // good information on the CMD file format

            byte code;
            int length;

            FilePath = Path;

            if (File.Exists(FilePath))
            {
                try
                {
                    if (Storage.LoadBinaryFile(FilePath, out byte[] b))
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
            }
            Finalize(false);
        }
        public bool Load(IMemory Memory)
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
    }
}
