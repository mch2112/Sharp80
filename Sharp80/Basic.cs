/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    /// <summary>
    /// This is broken; don't use it yet
    /// </summary>
    internal class Basic
    {
        
        private string[] Tokens =
        {
            "END", "FOR", "RESET", "SET", "CLS", "CMD", "RANDOM", "NEXT", "DATA",
            "INPUT", "DIM", "READ", "LET", "GOTO", "RUN", "IF", "RESTORE", "GOSUB",
            "RETURN", "REM", "STOP", "ELSE", "TRON", "TROFF", "DEFSTR", "DEFINT", "DEFSNG",
            "DEFDBL", "LINE", "EDIT", "ERROR", "RESUME", "OUT", "ON", "OPEN",
            "FIELD", "GET", "PUT", "CLOSE", "LOAD", "MERGE", "NAME", "KILL",
            "LSET", "RSET", "SAVE", "SYSTEM", "LPRINT", "DEF", "POKE", "PRINT", "CONT",
            "LIST", "LLIST", "DELETE", "AUTO", "CLEAR", "CLOAD", "CSAVE", "NEW",
            "TAB(", "TO", "FN", "USING", "VARPTR", "USR", "ERL", "ERR", "STRING$", "INSTR", "POINT", "TIME$", "MEM", "INKEY$", "THEN", "NOT", "STEP",
            "+", "-", "*", "/", "[", "AND", "OR", ">", "=", "<", "SGN", "INT",
            "ABS", "FRE", "INP", "POS", "SQR", "RND", "LOG", "EXP", "COS", "SIN",
            "TAN", "ATN", "PEEK", "CVI", "CVS", "CVD", "EOF", "LOC", "LOF", "MKI$",
            "MKS$", "MKD$", "CINT", "CSNG", "CDBL", "FIX", "LEN", "STR$", "VAL",
            "ASC", "CHR$", "LEFT$", "RIGHT$", "MID$"
        };

        public string Listing { get; private set; } = "";

        enum Mode { Normal, DataLine, Rem, RemQuote }

        Mode mode = Mode.Normal;
        public Basic()
        {
            if (IO.LoadBinaryFile(@"C:\users\name\Desktop\foo.bas", out byte[] file))
            {
                StringBuilder sb = new StringBuilder();
                int cursor = 0;

                if (file[cursor++] != 255)
                {
                    Debug.WriteLine("missing magic # of 255 -- not a BASIC file.");
                    return;
                }

                while (cursor < file.Length - 4)
                {
                    cursor += 2; // ignore the address

                    int lineNum = Lib.CombineBytes(file[cursor++], file[cursor++]);

                    sb.Append($"{lineNum} ");

                    byte b;
                    while ((b = file[cursor++]) > 0)
                    {
                        if (b > 0x7F && b < Tokens.Length + 0x80)
                        {
                            switch (b)
                            {
                                case 136:
                                    mode = Mode.DataLine;
                                    break;
                                case 147:
                                    mode = Mode.Rem;
                                    break;
                            }
                            sb.Append(Tokens[b - 0x80]);
                        }
                        else
                        {
                            switch (b)
                            {
                                case 251:
                                    mode = Mode.RemQuote;
                                    break;
                                default:
                                    if (b >= ' ' && b <= 'z')
                                        sb.Append((char)b);
                                    else
                                        sb.Append($"[{b:X2}]");
                                    break;
                            }
                        }
                    }
                    sb.AppendLine();
                }
                Listing = sb.ToString();
            }
            if (mode == Mode.Normal)
                return;
        }
    }
}
