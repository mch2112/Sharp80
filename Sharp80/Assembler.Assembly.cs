using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace Sharp80.Assembler
{
    public enum Status { New, Empty, AssembleFailed, AssembleDone, IntWriteFailed, CmdWriteFailed, Complete, CompleteOK}

    internal class Assembly
    {
        public string CmdFilePath { get; private set; }
        public string IntFilePath { get; private set; }

        public string Title { get; private set; }

        public string SourceText { get; private set; }
        public IEnumerable<string> SourceLines => SourceText.Split(new string[] { Environment.NewLine, "\n", "\r"}, StringSplitOptions.None);
        public string IntermediateOutput { get; private set; }

        public ushort ExecAddress { get; private set; }
        public int NumErrors { get; private set; }

        public bool AssembledOK => Status == Status.AssembleDone || Status == Status.IntWriteFailed || Status == Status.CmdWriteFailed || Status == Status.Complete || Status == Status.CompleteOK;
        public bool IntFileWritten => Status == Status.Complete || Status == Status.CompleteOK;
        public bool CmdFIleWritten => Status == Status.CompleteOK;

        private Status Status { get; set; }

        private List<Assembler.LineInfo> Lines { get; set; }
        private List<(ushort SegmentAddress, byte[] Bytes)> Segments { get; set; }
        private Dictionary<string, Assembler.LineInfo> SymbolTable { get; set; }

        public Assembly(string SourceText)
        {
            this.SourceText = SourceText;
        }
        internal void Finalize(string Title, IEnumerable<Assembler.LineInfo> Lines, Dictionary<String, Assembler.LineInfo> SymbolTable, ushort? ExecAddress)
        {
            this.Title = Title;
            this.Lines = Lines.ToList();
            this.SymbolTable = SymbolTable;
            Segmentize();
            if (Segments.Count == 0)
            {
                Status = Status.Empty;
            }
            else
            {
                this.ExecAddress = ExecAddress ?? Segments.Min(s => s.SegmentAddress);
                NumErrors = Lines.Count(l => l.HasError);
                if (NumErrors > 0)
                    Status = Status.AssembleFailed;
            }
        }
        
        public CmdFile ToCmdFile()
        {
            if (Status == Status.CompleteOK)
                return new CmdFile(CmdFilePath);
            else
                return null;
        }
        public void Write(string CmdPath)
        {
            this.CmdFilePath = CmdPath;
            IntFilePath = Path.ChangeExtension(CmdPath, ".int.txt");
            switch (Status)
            {
                case Status.New:
                case Status.Empty:
                    break;
                case Status.Complete:
                case Status.AssembleFailed:
                case Status.IntWriteFailed:
                    if (WriteIntFile())
                        Status = Status.Complete;
                    else
                        Status = Status.IntWriteFailed;
                    break;
                case Status.CompleteOK:
                case Status.AssembleDone:
                case Status.CmdWriteFailed:
                    if (WriteIntFile() && WriteCmdFile())
                        Status = Status.CompleteOK;
                    else
                        Status = Status.CmdWriteFailed;
                    break;
            }
        }
        
        private bool WriteIntFile()
        {
            try
            {
                Storage.SaveTextFile(IntFilePath,
                                     string.Join(Environment.NewLine,
                                                 Lines.Select(lp => string.Format("{0:00000} {1}", lp.SourceFileLine, lp.FullNameWithOriginalLineAsCommentWithErrorIfAny))) +
                                     Environment.NewLine +
                                     Environment.NewLine +
                                     SymbolTableToString());
            }
            catch
            {
                return false;
            }
            return true;
        }

        private string SymbolTableToString()
        {
            return "SYMBOL TABLE" + Environment.NewLine +
                   "==================================" + Environment.NewLine +
                   String.Join(Environment.NewLine,
                               SymbolTable.OrderBy(kv => kv.Key)
                                          .Select(kv => kv.Key.PadRight(15) +
                                                        kv.Value.Address.ToHexString() +
                                                        string.Format(" (Line {0}, {1})", kv.Value.SourceFileLine, kv.Value.Mnemonic)));
        }
        private void Segmentize()
        {
            if (Status == Status.New)
            {
                try
                {
                    byte[] buffer = new byte[0x10000];
                    var data = new List<(ushort SegmentAddress, byte[] Bytes)>();
                    int lineNum = 0;

                    while (LoadToBuffer(ref lineNum, buffer, out ushort lowAddress, out ushort highAddress))
                    {
                        var segment = new byte[highAddress - lowAddress];
                        Array.Copy(buffer, lowAddress, segment, 0, segment.Length);

                        data.Add((lowAddress, segment));
                    }
                    Segments = data;
                    Status = Status.AssembleDone;
                }
                catch (Exception ex)
                {
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, "Failed to save CMD file from assembler.");
                    Status = Status.AssembleFailed;
                    Segments = null;
                }
            }
        }
        private bool WriteCmdFile()
        {
            try
            {
                Title = Title ?? MakeTitle(Path.GetFileNameWithoutExtension(CmdFilePath));

                var writer = new BinaryWriter(File.Open(CmdFilePath, FileMode.Create));

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

                foreach (var d in Segments)
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
                ExecAddress.Split(out lowDest, out highDest);
                writer.Write(lowDest);
                writer.Write(highDest);

                writer.Close();
                return true;
            }
            catch
            {
                this.Status = Status.CmdWriteFailed;
                return false;
            }
        }

        /// <summary>
        /// Range is inclusive with low address, exclusive with highaddress
        /// </summary>
        private bool LoadToBuffer(ref int LineNumber, byte[] Buffer, out ushort lowAddress, out ushort highAddress)
        {
            lowAddress = 0xFFFF;
            highAddress = 0x0000;
            bool any = false;

            while (LineNumber < Lines.Count)
            {
                var lp = Lines[LineNumber++];

                if (lp.IsOrg && any)
                {
                    LineNumber--;
                    break;
                }
                if (lp.Size > 0)
                {
                    any = true;
                    lowAddress = Math.Min(lowAddress, lp.Address);
                    highAddress = Math.Max(highAddress, lp.Address.Offset(lp.Size));
                }

                if (lp.Size > 0)
                    Debug.Assert(lp.Byte0.HasValue);
                if (lp.Size > 1)
                    Debug.Assert(lp.Byte1.HasValue);
                else
                    Debug.Assert(!lp.Byte1.HasValue);
                if (lp.Size > 2)
                    Debug.Assert(lp.Byte2.HasValue);
                else
                    Debug.Assert(!lp.Byte2.HasValue);
                if (lp.Size > 3)
                    Debug.Assert(lp.Byte3.HasValue);
                else
                    Debug.Assert(!lp.Byte3.HasValue);

                if (lp.Byte0.HasValue) Buffer[lp.Address] = lp.Byte0.Value;
                if (lp.Byte1.HasValue) Buffer[lp.Address + 1] = lp.Byte1.Value;
                if (lp.Byte2.HasValue) Buffer[lp.Address + 2] = lp.Byte2.Value;
                if (lp.Byte3.HasValue) Buffer[lp.Address + 3] = lp.Byte3.Value;
            }
            return any;
        }
        private static string MakeTitle(string FileName)
        {
            FileName = FileName.ToUpper();
            return String.Join(String.Empty, FileName.Where(c => c.IsBetween('A', 'Z')).Take(CmdFile.MAX_TITLE_LENGTH));
        }
    }
}
