using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Sharp80.Z80.Assembler
{
    internal enum Status { New, Empty, AssembleFailed, AssembleOK }

    public class Assembly
    {
        public string Title { get; private set; }
        public string SourceText { get; private set; }
        public ushort ExecAddress { get; private set; }
        public int NumErrors { get; private set; }
        public List<(ushort SegmentAddress, byte[] Bytes)> Segments { get; private set; }

        public bool AssembledOK => Status == Status.AssembleOK;
        public IEnumerable<string> SourceLines => SourceText.Split(new string[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.None);

        private List<Assembler.LineInfo> Lines { get; set; }
        private Status Status { get; set; }
        private Dictionary<string, Assembler.LineInfo> SymbolTable { get; set; }

        internal Assembly(string SourceText)
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
        public string IntermediateOutput
        {
            get
            {
                if (Status == Status.New || Status == Status.Empty)
                    return String.Empty;
                else
                    return string.Join(Environment.NewLine,
                                       Lines.Select(lp => string.Format("{0:00000} {1}", lp.SourceFileLine, lp.FullNameWithOriginalLineAsCommentWithErrorIfAny))) +
                                       Environment.NewLine +
                                       Environment.NewLine +
                                       SymbolTableToString();
            }
        }
        private string SymbolTableToString()
        {
            return "SYMBOL TABLE" + Environment.NewLine +
                   "============================================" + Environment.NewLine +
                   String.Join(Environment.NewLine,
                               SymbolTable.OrderBy(kv => kv.Key)
                                          .Select(kv => kv.Key.PadRight(Assembler.MAX_LABEL_LENGTH + 1) + " " + kv.Value.SymbolTableReference));
        }
        private void Segmentize()
        {
            if (Status == Status.New)
            {
                try
                {
                    byte[] buffer = new byte[Z80.MEMORY_SIZE];
                    var data = new List<(ushort SegmentAddress, byte[] Bytes)>();
                    int lineNum = 0;

                    while (LoadToBuffer(ref lineNum, buffer, out ushort lowAddress, out ushort highAddress))
                    {
                        var segment = new byte[highAddress - lowAddress];
                        Array.Copy(buffer, lowAddress, segment, 0, segment.Length);

                        data.Add((lowAddress, segment));
                    }
                    Segments = data;
                    Status = Status.AssembleOK;
                }
                catch (Exception Ex)
                {
                    Status = Status.AssembleFailed;
                    Segments = null;
                    throw Ex;
                }
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
    }
}
