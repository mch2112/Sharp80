/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80.Assembler
{
    internal partial class Assembler
    {
        private class LineInfo
        {
            public string RawLine { get; private set; }
            public string Label { get; private set; }
            public string Mnemonic { get; private set; }
            public string Comment { get; private set; }

            public List<Operand> Operands = new List<Operand>();

            public Operand Operand0 { get { return Operands.Count > 0 ? Operands[0] : new Operand(); } }
            public Operand Operand1 { get { return Operands.Count > 1 ? Operands[1] : new Operand(); } }

            public bool IsMultiline { get { return IsMetaInstruction && NumOperands > 1; } }
            public bool IsOrg { get; set; } = false;
            public int NumOperands { get { return Operands.Count(o => o.Exists); } }

            public ushort Address = 0x0000;
            public byte? Byte0 = null;
            public byte? Byte1 = null;
            public byte? Byte2 = null;
            public byte? Byte3 = null;

            public int SourceFileLine { get; private set; }
            public bool IsSuppressed { get; private set; } = false;
            public string Error { get; private set; } = null;
            public void SetError(string ErrMsg)
            {
                if (String.IsNullOrWhiteSpace(Error))
                    Error = ErrMsg;
                else
                    Error += Environment.NewLine + ErrMsg;
            }

            public bool Valid { get { return !IsSuppressed && !HasError && (HasLabel || HasMnemonic); } }
            public bool Empty {  get { return !HasLabel && !HasMnemonic && !HasComment; } }
            public bool HasLabel { get { return Label.Length > 0; } }
            public bool HasMnemonic { get { return Mnemonic.Length > 0; } }
            public bool HasComment { get { return Comment.Length > 0; } }
            public bool CommentOnly { get { return HasComment || !Valid; } }

            public Processor.Instruction Instruction { get; set; } = null;
            public Dictionary<string, LineInfo> SymbolTable { get; private set; }

            public LineInfo(string RawLine, int SourceFileLine, Dictionary<string, LineInfo> SymbolTable)
            {
                string text = this.RawLine = RemoveSpaces(RawLine);

                this.SourceFileLine = SourceFileLine;
                this.SymbolTable = SymbolTable;

                Label = String.Empty;
                Mnemonic = String.Empty;

                if (text.Contains(";"))
                {
                    Comment = text.Substring(text.IndexOf(';') + 1);
                    text = text.Substring(0, text.IndexOf(';')).TrimEnd();
                }
                else
                {
                    Comment = String.Empty;
                }

                string[] cols = text.Split('\t').Select(c => c.Trim()).ToArray();

                if (cols.Length == 0)
                    return;

                // LABEL

                string label = cols[0];
                if (label.Length == 0)
                {
                }
                else if (ValidateLabel(ref label))
                {
                    Label = label;
                }
                else
                {
                    Error = "Invalid Label: " + label;
                    return;
                }

                // MNEMONIC

                if (cols.Length > 1)
                {
                    string mnemonic = cols[1];
                    if (mnemonic.Length == 0)
                    {
                    }
                    else if (ValidateMnemonic(ref mnemonic))
                    {
                        Mnemonic = mnemonic;
                    }
                    else
                    {
                        Error = "Invalid Mnemonic: " + mnemonic;
                        return;
                    }
                }

                // OPERANDS

                if (cols.Length > 2)
                {
                    string[] ops = GetCSV(cols[2], 1000);

                    foreach (var o in ops)
                    {
                        Operands.Add(new Operand(this, o.Trim()));
                    }
                }

                if (IsMultiline)
                {
                    switch (Mnemonic)
                    {
                        case "DEFB":
                        case "DEFW":
                        case "DEFM":
                            break;
                        default:
                            Error = "Unexpected number of operands";
                            break;
                    }
                }
                else
                {
                    switch (this.Mnemonic)
                    {
                        case "ADD":
                        case "ADC":
                        case "SUB":
                        case "SBC":
                        case "AND":
                        case "OR":
                        case "XOR":
                        case "CP":
                            // Change lines like "ADD A, B" to "ADD B"
                            TrimAccumlatorOperand(0);
                            break;
                    }
                }
            }
            public bool HasError
            {
                get
                {
#if DEBUG
                    if (IsSuppressed && !String.IsNullOrWhiteSpace(Error))
                        throw new Exception("Suppressed lines should never have errors.");
#endif
                    return !String.IsNullOrWhiteSpace(Error); }
            }
            private void TrimAccumlatorOperand(int OpNum)
            {
                if (NumOperands == 2 && Operands[OpNum].IsAccumulator)
                    Operands.Remove(Operands[OpNum]);
            }
            public bool IsMetaInstruction
            {
                get { return metaInstructions.Contains(Mnemonic); }
            }
            public bool IsInstruction
            {
                get { return instructionNames.Contains(Mnemonic); }
            }
            public string FullName
            {
                get
                {
                    string bytes = String.Empty;

                    if (Byte0.HasValue)
                        bytes += Byte0.Value.ToHexString() + SPACE;
                    if (Byte1.HasValue)
                        bytes += Byte1.Value.ToHexString() + SPACE;
                    if (Byte2.HasValue)
                        bytes += Byte2.Value.ToHexString() + SPACE;
                    if (Byte3.HasValue)
                        bytes += Byte3.Value.ToHexString() + SPACE;

                    bytes = bytes.PadRight(13);

                    bytes += Label;

                    bytes = bytes.PadRight(26);

                    return Address.ToHexString() +
                           ":  " +
                           bytes +
                           Mnemonic +
                           (Operand0.Exists ? (SPACE + Operand0.ToString()) + (Operand1.Exists ? ", " + Operand1.ToString()
                                                                                               : String.Empty)
                                            : String.Empty);
                }
            }
            public string FullNameWithOriginalLineAsCommentWithErrorIfAny
            {
                get
                {
                    if (IsSuppressed)
                        return "; " + RawLine;
                    else if (HasError)
                        return Environment.NewLine +
                               RawLine + Environment.NewLine + "ERROR: " + Error +
                               Environment.NewLine;
                    else if (CommentOnly)
                        return RawLine.Trim().PadLeft(26);

                    var s = new StringBuilder();
                    if (RawLine.Length > 0)
                        s.Append(FullName.PadRight(51) + "; " + RawLine);
                    else
                        s.Append(FullName);
                    return s.ToString();
                }
            }
            public int Size
            {
                get
                {
                    if (!Valid)
                        return 0;
                    else if (Instruction != null)
                        return Instruction.Size;
                    else switch (Mnemonic)
                        {
                            case "":
                            case "ORG":
                            case "EQU":
                            case "END":
                                return 0;
                            case "DEFB":
                                return 1;
                            case "DEFW":
                                return 2;
                            case "DEFS":
                                if (Operand0.NumericValue.HasValue)
                                    return Operand0.NumericValue.Value;
                                else
                                    throw new Exception("DEFS requires a numeric value");
                            case "DEFM":
                                return Operand0.RawText.Length;
                            default:
                                throw new Exception("Error in LineInfo Size calc");
                        }
                }
            }
            public int OpcodeSize
            {
                get
                {
                    if (Instruction != null)
                        return Instruction.OpcodeLength;
                    else
                        return 0;
                }
            }
            public int DataSize
            {
                get
                {
                    return Size - OpcodeSize;
                }
            }

            /// <summary>
            /// Suppressed lines show up in intermediate files but aren't compiled
            /// </summary>
            public void Suppress() { IsSuppressed = true; }

            private bool ValidateLabel(ref string Label)
            {
                System.Diagnostics.Debug.Assert(Label == Label.Trim().ToUpper());

                if (Label.StartsWith("."))
                    Label = Label.Substring(1);

                if (Label.EndsWith(":"))
                    Label = Label.Substring(0, Label.Length - 1);

                return Label.Length < MAX_LABEL_LENGTH &&
                        Label.Length >= MIN_LABEL_LENGTH &&
                        Label[0] >= 'A' &&
                        Label[0] <= 'Z' &&
                        Label.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' || c <= '9') || c == '_');
            }
            private bool ValidateMnemonic(ref string Mnemonic)
            {
                if (Mnemonic == "DW")
                    Mnemonic = "DEFW";
                else if (Mnemonic == "DB")
                    Mnemonic = "DEFB";
                else if (Mnemonic == "DM")
                    Mnemonic = "DEFM";
                else if (Mnemonic == "DS")  // Maybe add 'block', 'rmem' to this list
                    Mnemonic = "DEFS";

                return IsMetaInstruction(Mnemonic) ||
                       IsInstruction(Mnemonic);
            }
            public override string ToString()
            {
                return FullNameWithOriginalLineAsCommentWithErrorIfAny;
            }
        }
    }
}
