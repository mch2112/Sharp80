using System;
using System.Diagnostics;
using System.Linq;

namespace Sharp80.Assembler
{
    internal partial class Assembler
    {
        private class Operand
        {
            public LineInfo LineInfo { get; private set; }
            public string RawText { get; private set; }
            public bool Exists
            {
                get { return RawText.Length > 0; }
            }

            public bool IsIndirect { get; private set; }
            public bool IsPseudoIndirect
            {
                get
                {
                    return IsIndirect &&
                        LineInfo.Mnemonic == "JP" || LineInfo.Mnemonic == "OUT" || LineInfo.Mnemonic == "IN";
                }
            }

            public Operand()
            {
                RawText = String.Empty;
                Debug.Assert(!Exists);
            }
            public Operand(LineInfo LineInfo, string RawText)
            {
                this.LineInfo = LineInfo;

                this.RawText = RawText;

                if (this.RawText.Length == 0)
                    return;

                if (IsInParentheses(this.RawText))
                {
                    IsIndirect = true;
                    this.RawText = RemoveParentheses(this.RawText);
                }

                if (IsRegister)
                {
                    if (IsIndexedRegister && IsIndirect && !IsPseudoIndirect)
                    {
                        // We know this is a bare indirect IX or IY register
                        IndexDisplacement = 0;
                        indexDisplacementRaw = String.Empty;
                    }
                }
                else if (!IsFlagState)
                {
                    if (IsIndirect && IsIndexedRegister)
                    {
                        // We know this is an indexed indirect IX or IY register
                        Debug.Assert(this.RawText.Contains(PLUS_SIGN) || this.RawText.Contains(MINUS_SIGN));

                        int signLoc = this.RawText.IndexOf(PLUS_SIGN);
                        if (signLoc >= 0)
                            indexDisplacementRaw = this.RawText.Substring(signLoc + 1);
                        else
                        {
                            signLoc = this.RawText.IndexOf(MINUS_SIGN);
                            indexDisplacementRaw = this.RawText.Substring(signLoc);
                        }
                    }
                }
                switch (this.LineInfo.Mnemonic)
                {
                    // don't require parenthesis
                    case "OUT":
                        if (this == this.LineInfo.Operand0)
                            IsIndirect = true;
                        break;
                    case "IN":
                        if (this == this.LineInfo.Operand1)
                            IsIndirect = true;
                        break;
                    case "JP":
                        if (this.RawText == "HL" || this.RawText == "IX" || this.RawText == "IY")
                            IsIndirect = true;
                        break;
                }
            }

            public bool IsRegister
            {
                get { return registers.Contains(RawText); }
            }
            public bool IsAccumulator
            {
                get { return RawText == "A"; }
            }
            public bool IsIndexedRegister
            {
                get
                {
                    return RawText.StartsWith("IX+") ||
                           RawText.StartsWith("IY+") ||
                           RawText.StartsWith("IX-") ||
                           RawText.StartsWith("IY-") ||
                           RawText == "IX" ||
                           RawText == "IY";
                }
            }
            public bool IsFlagState => flagStates.Contains(this.RawText);

            private byte? _indexDisplacement = null; // for IX and IY displacements
            private string indexDisplacementRaw = String.Empty;
            public byte? IndexDisplacement
            {
                get
                {
                    if (_indexDisplacement.HasValue)
                        return _indexDisplacement;

                    if (IsIndirect && IsIndexedRegister && !IsPseudoIndirect)
                    {
                        if (indexDisplacementRaw.Length == 0)
                            _indexDisplacement = 0;
                        else
                        {
                            _indexDisplacement = (byte?)GetSymbolValue(LineInfo.SymbolTable, LineInfo, indexDisplacementRaw);
                        }

                        return _indexDisplacement;
                    }
                    else
                    {
                        return null;
                    }
                }
                private set { _indexDisplacement = value; }
            }

            // Data includes numeric values, index offsets, but not values implied in the opcode itself
            public bool HasData
            {
                get
                {
                    if (!Exists)
                        return false;

                    if (ArgImpliedInOpcode(LineInfo.Mnemonic))
                    {
                        switch (LineInfo.Mnemonic)
                        {
                            case "RST":
                            case "IM":
                                return false;
                            case "BIT":
                            case "RES":
                            case "SET":
                                return IsNumeric && LineInfo.Operand1 == this;
                            default:
                                throw new Exception();
                        }
                    }
                    else
                    {
                        return IsNumeric;
                    }
                }
            }

            public bool IsNumeric => !IsRegister && !IsFlagState && !IsIndexedRegister;

            public ushort? NumericValue
            {
                get
                {
                    if (!IsNumeric)
                        throw new Exception(string.Format("Numeric Value Expected; found {0}", RawText));

                    return GetNumericValue(RawText) ?? GetSymbolValue(LineInfo.SymbolTable, LineInfo, RawText);
                }
            }
            public string Text
            {
                get
                {
                    if (IsNumeric)
                    {
                        var val = NumericValue;
                        if (val.HasValue)
                            return val.Value.ToHexString();
                    }
                    return RawText;
                }
            }
            public (byte Low, byte High) GetDataBytes()
            {
                if (NumericValue.HasValue)
                {
                    NumericValue.Value.Split(out byte? Low, out byte? High);
                    return (Low.Value, High.Value);
                }
                else
                    return (0, 0);
            }
            public override string ToString()
            {
                string text = RawText;

                string index = String.Empty;

                if (IndexDisplacement.HasValue)
                {
                    if ((IndexDisplacement.Value & 0x80) > 0)
                        index = " - " + Math.Abs((sbyte)(IndexDisplacement.Value)).ToHexString();
                    else
                        index = " + " + IndexDisplacement.Value.ToHexString();
                    text = text.Replace("+D", index);
                }
                else if (IsNumeric)
                {
                    if (LineInfo.Operand0 == this && SingleDigitArg(LineInfo.Mnemonic))
                    {
                        text = NumericValue.Value.ToHexChar();
                    }
                    else
                    {
                        switch (LineInfo.DataSize)
                        {
                            case 1:
                                text = ((byte)NumericValue.Value).ToHexString();
                                break;
                            case 0: // meta instructions
                            case 2:
                                text = NumericValue.Value.ToHexString();
                                break;
                        }
                    }
                }

                if (IsIndirect)
                    text = Parenthesize(text);

                return text;
            }
        }
    }
}
