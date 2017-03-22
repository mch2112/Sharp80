using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sharp80
{
    internal class CmdFile
    {
        public ushort? ExecAddress { get; private set; } = null;

        public bool Valid { get; private set; } = false;
        public string FilePath { get; private set; }
        private List<(ushort SegmentAddress, byte[] Bytes)> blocks = new List<(ushort SegmentAddress, byte[] Bytes)>();
        public int Size { get; private set; }
        public int NumBlocks {  get { return blocks.Count; } }
        public ushort LowAddress { get; private set; }
        public ushort HighAddress { get; private set; }
        public bool IsLoaded { get; private set; }
        public CmdFile(string Path)
        {
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
                                    blocks.Add((addr, data));
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
                                case 0x05: break;   // load module header
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
        internal bool Load(IMemory Memory)
        {
            if (Valid)
            {
                foreach (var b in blocks)
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
        private void Finalize(bool Valid)
        {
            this.Valid = Valid && blocks.Count > 0;
            if (Valid)
            {
                blocks = blocks.OrderBy(b => b.SegmentAddress).ToList();
                for (int i = 0; i < blocks.Count - 1; i++)
                {
                    // are these adjoining blocks?
                    if (blocks[i].SegmentAddress + blocks[i].Bytes.Length == blocks[i + 1].SegmentAddress)
                    {
                        // if so, combine them
                        byte[] data = new byte[blocks[i].Bytes.Length + blocks[i + 1].Bytes.Length];
                        Array.Copy(blocks[i].Bytes, 0, data, 0, blocks[i].Bytes.Length);
                        Array.Copy(blocks[i + 1].Bytes, 0, data, blocks[i].Bytes.Length, blocks[i + 1].Bytes.Length);
                        blocks[i] = (blocks[i].SegmentAddress, data);
                        blocks.RemoveAt(i + 1);
                        i--;
                    }
                }
                Size = blocks.Sum(b => b.Bytes.Length);
                Valid &= Size > 0;
                LowAddress = blocks[0].SegmentAddress;
                HighAddress = (ushort)(blocks.Last().SegmentAddress + blocks.Last().Bytes.Length - 1);
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
