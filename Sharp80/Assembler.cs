/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Sharp80.Assembler
{
    internal partial class Assembler
    {
        private List<Processor.Instruction> instructionSet;

        private readonly List<LineInfo> unit = new List<LineInfo>();
        private Dictionary<string, LineInfo> symbolTable = new Dictionary<string, LineInfo>();

        private const char SINGLE_QUOTE = '\'';
        private const char PLUS_SIGN = '+';
        private const char MINUS_SIGN = '-';
        private const char SPACE = ' ';

        private const int MAX_LABEL_LENGTH = 12;
        private const int MIN_LABEL_LENGTH = 2;

        private static string[] registers = new string[] { "A", "B", "C", "D", "E", "F", "H", "L",
                                                       "AF", "BC", "DE", "HL",
                                                       "IXh", "IXl", "IYh", "IYl",
                                                       "I", "R", "PC", "SP",
                                                       "AF'", "BC'", "DE'", "HL'",
                                                       "IX", "IY",
                                                       "(HL)", "(BC)", "(DE)", "(AF)" };

        private static string[] sixteenBitRegisters = new string[] { "AF", "BC", "DE", "HL", "IX", "IY", "SP" };
        private static string[] metaInstructions = new string[] { "ORG", "EQU", "DEFB", "DB", "DEFW", "DW", "DEFM", "DM", "DEFS", "DS", "END" };
        private static string[] flagStates = new string[] { "C", "NC", "Z", "NZ", "PO", "PE", "P", "M" };
        private static string[] instructionNames = new string[] { "ADC", "ADD", "AND", "BIT", "CALL", "CCF", "CP", "CPD", "CPDR", "CPI", "CPIR", "CPL", "DAA",
                                                                  "DEC", "DI", "DJNZ", "EI", "EX", "EXX", "HALT", "IM", "IN", "INC", "IND", "INDR", "INI", "INIR", "JP", "JR",
                                                                  "LD", "LDD", "LDDR", "LDI", "LDIR", "NEG", "NOP", "OR", "OTDR", "OTIR", "OUT", "OUTD", "OUTI", "OUTR",
                                                                  "POP", "PUSH", "RES", "RET", "RETI", "RETN", "RL", "RLA", "RLC", "RLCA", "RLD", "RR", "RRC", "RRCA",
                                                                  "RRD", "RST", "SBC", "SCF", "SET", "SLA", "SRA", "SRL", "SUB", "XOR" };

        private string originalFilePath;
        private string intermediateFile = String.Empty;

        private List<Macro> macros = new List<Macro>();
        private ushort? execAddress = null;

        public Assembler(IEnumerable<Processor.Instruction> InstructionSet)
        {
            instructionSet = InstructionSet.ToList();
        }

        public string Assemble()
        {
            string cmdFilePath = string.Empty;

            if (GetFilePath())
            {
                try
                {
                    cmdFilePath = Assemble(out int errs);
                    if (errs == 0)
                        Dialogs.InformUser(string.Format("Assembled {0} to {1}.", Path.GetFileName(originalFilePath), cmdFilePath));
                    else if (Dialogs.AskYesNo(string.Format("{0} error{1} found during assembly. Open intermediate file?",
                                                            errs,
                                                            errs == 1 ? String.Empty : "s")))
                            Dialogs.ShowTextFile(intermediateFile);
                }
                catch (Exception ex)
                {
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"Failed to assemble {Path.GetFileName(originalFilePath)}.");
                }
            }
            return cmdFilePath;
        }
        private string Assemble(out int ErrCount)
        {
            Log.LogDebug("Assembling " + originalFilePath + "...");

            LoadAssemblyFile(originalFilePath);

            DetermineOpcodes();
            CalcAddresses();
            ResolveSymbols();
            DetermineData();
            SaveIntermediateFile();

            ErrCount = unit.Count(l => l.HasError);

            if (ErrCount == 0)
                return SaveAsCMD(unit);
            else
                return string.Empty;
        }

        private bool GetFilePath()
        {
            string path = Settings.LastAsmFile;

            path = Dialogs.GetAssemblyFile(path, false);

            if (path.Length > 0)
            {
                Settings.LastAsmFile = originalFilePath = path;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void LoadAssemblyFile(string Filename)
        {
            var lines = Storage.LoadTextFile(Filename);

            int sourceFileLine = 0;
            Macro m = null;

            for (int i = 0; i < lines.Count; i++)
            {
                sourceFileLine++;

                string line = PreprocessLine(lines[i]);

                if (!string.IsNullOrWhiteSpace(line))
                {
                    string mn = GetCol(line, 1);

                    if (m != null) // are we currently loading a macro?
                    {
                        if (mn == "ENDM")
                            m = null;
                        else if (!String.IsNullOrWhiteSpace(line))
                            m.AddLine(line);
                    }
                    else if (mn == "MACRO") // is this a new macro?
                    {
                        macros.Add(m = new Macro(line));
                        AddLine("; Macro " + m.Name, sourceFileLine);
                    }
                    else if ((m = GetMacro(mn)) != null)
                    {
                        // macros convert a delimited set of tokens each to an instruction
                        foreach (string l in m.Expand(GetCol(line, 2), sourceFileLine, out string Error))
                            AddLine(l, sourceFileLine, null, Error);
                        m = null;
                    }
                    else
                    {
                        AddLine(line, sourceFileLine);
                    }
                }
            }
        }
        private static string RemoveSpaces(string Input) { return Input.Trim(new char[] { ' ' }); }

        private static string GetCol(string Line, int ColNum)
        {
            string[] lines = Line.Split('\t');

            if (ColNum < lines.Length)
                return lines[ColNum].Trim();
            else
                return String.Empty;
        }
        private static string[] GetCSV(string Str, int MaxCols)
        {
            bool quoting = false;
            List<string> vals = new List<string>();
            char c;
            int stringStart = 0;
            bool escaping = false;

            for (int i = 0; i < Str.Length && vals.Count < MaxCols; i++)
            {
                c = Str[i];

                if (c == '\\')
                {
                    escaping = !escaping;
                    if (escaping)
                        continue;
                }
                if (c == SINGLE_QUOTE && !escaping)
                {
                    quoting = !quoting;
                }
                if (c == ',')
                {
                    if (!quoting)
                    {
                        vals.Add(Str.Substring(stringStart, i - stringStart));
                        stringStart = i + 1;
                    }
                }
            }

            vals.Add(Str.Substring(stringStart));

            return vals.Select(v => v.Trim()).ToArray();
        }

        private void AddLine(string rawLine, int SourceFileLine, List<LineInfo> Lines = null, string Error = null)
        {
            Lines = Lines ?? unit;

            LineInfo lp;
            List<LineInfo> linesToAdd = new List<LineInfo>();

            lp = new LineInfo(rawLine, SourceFileLine, symbolTable);

            if (!String.IsNullOrWhiteSpace(Error))
                lp.SetError(Error);

            if (lp.Empty)
                return;

            // Expand multivalue meta instructions
            string label = lp.Label;
            string comment = String.IsNullOrWhiteSpace(lp.Comment) ? String.Empty : ("\t; " + lp.Comment);
            switch (lp.Mnemonic)
            {
                case "DEFB":
                case "DEFW":
                case "DEFM":
                    if (lp.IsMultiline)
                    {
                        comment = String.Empty;
                        foreach (var o in lp.Operands)
                        {
                            AddLine($"{label}\t{lp.Mnemonic}\t{o.RawText}\t{comment}",
                                    SourceFileLine,
                                    linesToAdd);
                            label = String.Empty;
                        }
                        lp.Suppress();
                    }
                    else if (IsInSingleQuotes(lp.Operand0.RawText))
                    {
                        string msg = lp.Operand0.RawText;

                        msg = Unquote(msg);

                        if ((lp.Mnemonic == "DEFB" && msg.Length != 1) || (lp.Mnemonic == "DEFW" && (msg.Length < 1 || msg.Length > 2)))
                        {
                            lp.SetError("Invalid size for " + lp.Mnemonic);
                        }
                        else
                        {
                            if (lp.Mnemonic == "DEFW" && msg.Length == 1)
                            {
                                // pad with zero byte
                                AddLine($"{label}\tDEFB\t00H{comment}",
                                        SourceFileLine,
                                        linesToAdd);
                                label = comment = String.Empty;
                            }
                            foreach (char c in msg)
                            {
                                AddLine(string.Format("{0}\tDEFB\t{1}H{2}",
                                                       label,
                                                       c.ToHexString(),
                                                       String.IsNullOrWhiteSpace(comment) ? string.Format(" ; '{0}'", c) : string.Format("{0}: '{1}'", comment, c)),
                                        SourceFileLine,
                                        linesToAdd);
                                label = comment = String.Empty;
                            }
                            lp.Suppress();
                        }
                    }
                    else if (lp.Mnemonic == "DEFM")
                    {
                        AddLine($"{label}\tDEFB\t{lp.Operand0.RawText}{comment}",
                                SourceFileLine,
                                linesToAdd);
                        lp.Suppress();
                    }
                    break;
                case "DEFS":
                    if (lp.Operand0.NumericValue.HasValue)
                    {
                        int bytes = lp.Operand0.NumericValue.Value;
                        for (int i = 0; i < bytes; i++)
                        {
                            AddLine($"{label}\tDEFB\t00H\t{comment}",
                                    SourceFileLine,
                                    linesToAdd);
                            label = comment = String.Empty;
                        }
                        lp.Suppress();
                    }
                    else
                    {
                        lp.SetError("DEFS requires a numeric argument.");
                    }
                    break;
            }

            if (lp.IsSuppressed)
                linesToAdd.Insert(0, lp);
            else
                linesToAdd.Add(lp);

            foreach (var l in linesToAdd)
            {
                Lines.Add(l);
                if (l.Valid && Lines == unit && l.Label.Length > 0)
                {
                    if (symbolTable.ContainsKey(l.Label))
                    {
                        l.SetError("Label already defined: " + l.Label);
                    }
                    else
                    {
                        symbolTable.Add(l.Label, l);
                    }
                }
            }
        }

        private bool CalcAddresses()
        {
            ushort address = 0;

            foreach (LineInfo lp in unit.Where(l => l.Valid))
            {
                if (lp.Mnemonic == "ORG")
                {
                    lp.IsOrg = true;
                    lp.Address = lp.Operand0.NumericValue.Value;
                    address = lp.Address;
                }
                else if (lp.Mnemonic == "EQU")
                {
                    lp.Address = address;
                }
                else if (lp.Mnemonic == "END")
                {
                    lp.Address = address;
                    if (lp.NumOperands > 0)
                        execAddress = lp.Operand0.NumericValue.Value;
                }
                else
                {
                    lp.Address = address;
                    address += (ushort)lp.Size;
                }
            }
            return true;
        }

        private bool GetMatchingInstruction(LineInfo lp)
        {
            if (!lp.IsMetaInstruction)
            {
                if (lp.Mnemonic.Length == 0)
                    return false;               // Not an instruction

                foreach (var i in instructionSet.Where(ii => ii.Mnemonic == lp.Mnemonic && ii.NumOperands == lp.NumOperands))
                {
                    bool ok = true;
                    for (int j = 0; j < lp.NumOperands; j++)
                    {
                        if (!DoOperandsMatch(lp, i, j))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        lp.Instruction = i;
                        return true;
                    }
                }

                lp.SetError($"Instruction not found: {lp.RawLine}");
            }
            return false;
        }

        private string GetLabel(string line)
        {
            string s = line;

            if (s.StartsWith("."))
                s = s.Substring(1);

            return GetCol(s, 0).Replace(":", String.Empty).Trim();
        }

        private static bool IsRegister(string s)           { return registers.Contains(s); }
        private static bool IsMetaInstruction(string inst) { return metaInstructions.Contains(inst); }
        private static bool IsInstruction(string inst)     { return instructionNames.Contains(inst); }
        private static bool IsFlagState(string s)          { return flagStates.Contains(s); }

        private string PreprocessLine(string Input)
        {
            string line = RemoveSpaces(Input.Substring(0, Math.Min(256, Input.Length)));

            var sb = new StringBuilder();
            bool commenting = false;
            bool quoting = false;
            bool escaping = false;

            // keep quoting from messing these up
            
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == ';' && !quoting)
                {
                    commenting = true;
                }
                if (!commenting && c == SINGLE_QUOTE && !escaping)
                {
                    // AF', BC', DE', HL' aren't quoted
                    if (c >= 2 && !IsPrimableRegister(line.Substring(i - 2, 2)))
                        quoting = !quoting;
                }

                if (quoting || commenting)
                    sb.Append(c);
                else
                    sb.Append(char.ToUpper(c));

                escaping = c == '\\';
            }

            if (quoting)
                sb.Append(SINGLE_QUOTE);

            return sb.ToString();
        }
        private bool IsPrimableRegister(string RegName)
        {
            Debug.Assert(RegName.Length == 2);
            switch (RegName.ToUpper())
            {
                case "AF":
                case "BC":
                case "DE":
                case "HL":
                    return true;
                default:
                    return false;
            }
        }
        private Macro GetMacro(string Name)
        {
            return macros.FirstOrDefault(m => m.Name == Name);
        }
        /// <summary>
        /// These instructions have arguments but they are specified by
        /// the opcode itself and don't need to be augmented with
        /// additional bytes
        /// </summary>
        private static bool ArgImpliedInOpcode(string Mnemonic)
        {
            switch (Mnemonic)
            {
                case "BIT":
                case "RES":
                case "SET":
                case "RST":
                case "IM":
                    return true;
                default:
                    return false;
            }
        }
        private static bool SingleDigitArg(string Mnemonic)
        {
            return Mnemonic == "BIT" ||
                   Mnemonic == "RES" ||
                   Mnemonic == "SET" ||
                   Mnemonic == "IM";
        }
        private void DetermineOpcodes()
        {
            foreach (var lp in unit.Where(l => l.Valid))
            {
                GetMatchingInstruction(lp);

                if (lp.Instruction != null)
                {
                    lp.Byte0 = lp.Instruction.Op0;
                    if (lp.Instruction.OpcodeLength > 1)
                        lp.Byte1 = lp.Instruction.Op1;
                    if (lp.Instruction.OpcodeLength > 2)
                        lp.Byte3 = lp.Instruction.Op3;
                }
            }
        }
        private void ResolveSymbols()
        {
            foreach (LineInfo lp in unit.Where(l => l.Valid))
            {
                for (int i = 0; i < lp.Operands.Count; i++)
                    if (lp.Operands[i].IsNumeric)
                        if (lp.Operands[i].NumericValue == null)
                            lp.SetError("Cannot resolve operand: " + lp.Operands[i].RawText);
            }
        }
        private void DetermineData()
        {
            foreach (LineInfo lp in unit.Where(l => l.Valid))
            {
                if (IsMetaInstruction(lp.Mnemonic))
                {
                    if (lp.Mnemonic == "DEFB")
                    {
                        lp.Byte0 = (byte)lp.Operand0.NumericValue;
                    }
                    else if (lp.Mnemonic == "DEFW")
                    {
                        var val = lp.Operand0.NumericValue;
                        lp.Byte0 = (byte)(val & 0x00FF);
                        lp.Byte1 = (byte)((val >> 8) & 0x00FF);
                    }
                }
                else
                {
                    Debug.Assert(!lp.Operand0.HasData || !lp.Operand1.HasData);

                    // IX + d; IY + d
                    bool byte2ContainsIndex = false;
                    if (lp.Operand0.IndexDisplacement.HasValue)
                    {
                        lp.Byte2 = (byte)lp.Operand0.IndexDisplacement;
                        byte2ContainsIndex = true;
                    }
                    else if (lp.Operand1.IndexDisplacement.HasValue)
                    {
                        lp.Byte2 = (byte)lp.Operand1.IndexDisplacement;
                        byte2ContainsIndex = true;
                    }
                    Operand operand;
                    if (lp.Operand0.HasData)
                        operand = lp.Operand0;
                    else if (lp.Operand1.HasData)
                        operand = lp.Operand1;
                    else
                        continue;

                    if (lp.Mnemonic == "JP" || lp.Mnemonic == "JR" || lp.Mnemonic == "DJNZ")
                    {
                        Debug.Assert(lp.OpcodeSize == 1);
                        switch (lp.DataSize)
                        {
                            case 1: // relative address only: 8 bit
                                int delta = operand.NumericValue.Value - lp.Address - 2;
                                if (delta > 0x7F || delta < -0x80)
                                {
                                    if (!lp.HasError)
                                        lp.SetError("Relative address too far.");
                                }
                                else
                                {
                                    lp.Byte1 = Lib.TwosCompInv((sbyte)(operand.NumericValue - lp.Address - 2));
                                }
                                break;
                            case 2:
                                (lp.Byte1, lp.Byte2) = operand.GetDataBytes();
                                break;
                            default:
                                throw new Exception();
                        }
                    }
                    else
                    {
                        switch (lp.DataSize)
                        {
                            case 0:
                                throw new Exception();
                            case 1:
                                switch (lp.Instruction.Size)
                                {
                                    case 2:
                                        lp.Byte1 = (byte)operand.NumericValue;
                                        break;
                                    case 3:
                                        if (byte2ContainsIndex)
                                            lp.Byte3 = (byte)operand.NumericValue;
                                        else
                                            lp.Byte2 = (byte)operand.NumericValue;
                                        break;
                                    case 4:
                                        lp.Byte3 = (byte)operand.NumericValue;
                                        break;
                                    default:
                                        throw new Exception();
                                }
                                break;
                            case 2:
                                if (byte2ContainsIndex)
                                {
                                    switch (lp.OpcodeSize)
                                    {
                                        case 1:
                                            throw new Exception();
                                        case 2:
                                            lp.Byte3 = (byte)operand.NumericValue;
                                            break;
                                        default:
                                            throw new Exception();
                                    }
                                }
                                else
                                { 
                                    switch (lp.OpcodeSize)
                                    {
                                        case 1:
                                            (lp.Byte1, lp.Byte2) = operand.GetDataBytes();
                                            break;
                                        case 2:
                                            (lp.Byte2, lp.Byte3) = operand.GetDataBytes();
                                            break;
                                        default:
                                            throw new Exception();
                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }
        private static ushort? GetSymbolValue(Dictionary<string, LineInfo> SymbolTable, LineInfo CurrentLP, string Symbol, ushort Offset = 0, bool ForceHex = false)
        {
            string displacement;
            ushort? sVal = 0;
            ushort? ret = null;

            if (Symbol.Length == 0)
                return null;

            if (Symbol == "$")
                if (CurrentLP != null)
                    return CurrentLP.Address.Offset(Offset);
                else
                    throw new Exception();

            int plusLoc, minusLoc;

            bool offsetChanged = false;

            string s = Symbol;
            // allow a plus or minus displacement from symbols
            do
            {
                plusLoc = s.LastIndexOf(PLUS_SIGN);
                minusLoc = s.LastIndexOf(MINUS_SIGN);

                if (plusLoc > minusLoc)
                {
                    displacement = s.Substring(plusLoc + 1);
                    s = s.Substring(0, plusLoc);
                    Offset += GetSymbolValue(SymbolTable, CurrentLP, displacement).Value;
                    offsetChanged = true;
                }
                else if (minusLoc > 1)
                {
                    displacement = s.Substring(minusLoc + 1);
                    s = s.Substring(0, minusLoc);
                    Offset -= GetSymbolValue(SymbolTable, CurrentLP, displacement).Value;
                    offsetChanged = true;
                }

            } while (plusLoc > 0 || minusLoc > 1);

            if (offsetChanged)
                return GetSymbolValue(SymbolTable, CurrentLP, s, Offset, ForceHex);

            sVal = GetNumericValue(s, ForceHex);
            if (sVal.HasValue)
            {
                ret = sVal.Value.Offset(Offset);
            }
            else if (SymbolTable.ContainsKey(s))
            {
                var symbolLine = SymbolTable[s];
                if (symbolLine.Mnemonic == "EQU")
                {
                    ret = symbolLine.Operand0.NumericValue;
                    ret += Offset;
                }
                else if (symbolLine.HasError)
                {
                    CurrentLP?.SetError($"Symbol {Symbol} defined on line {CurrentLP.SourceFileLine} which has an error.");
                }
                else
                {
                    ret = symbolLine.Address;
                    ret += Offset;
                }
            }
        
            if (!ret.HasValue && CurrentLP != null)
            {
                string err = $"Symbol {s} not found [Line {CurrentLP.SourceFileLine}].";
                Log.LogDebug(err);
                CurrentLP.SetError(err);
            }
            return ret;
        }
        private static string RemoveParentheses(string s)
        {
            if (IsInParentheses(s))
                return s.Substring(1, s.Length - 2).Trim();
            else
                return s.Trim();
        }
        private static string Parenthesize(string s)
        {
            if (!IsInParentheses(s))
                return '(' + s + ')';
            else
                return s;
        }
        private static bool IsInParentheses(string s)
        {
            return (s.Length >= 2 && s[0] == '(' && s[s.Length - 1] == ')');
        }
        private static bool IsInSingleQuotes(string s)
        {
            return (s.Length >= 2 && s[0] == '\'' && s[s.Length - 1] == '\'');
        }
        private static string Unquote(string Input)
        {
            if (IsInSingleQuotes(Input) && Input.Length > 1)
                return Input.Substring(1, Input.Length - 2);
            else
                return Input;
        }
        private static ushort? GetNumericValue(string Input, bool ForceHex = false)
        {
            string s = Input;

            bool negative = false;

            if (string.IsNullOrWhiteSpace(Input))
                return null;

            if (s[0] == '-')
            {
                negative = true;
                s = s.Substring(1);
            }

            // single character
            if (s.Length == 3 && IsInSingleQuotes(s))
                return (ushort)(negative ? -(int)s[1] : (int)s[1]);

            ushort i;

            // hexadecimal
            if (ForceHex || s.EndsWith("H"))
            {
                string hexString = s;
                if (hexString.EndsWith("H"))
                    hexString = hexString.Substring(0, s.Length - 1);

                if (ushort.TryParse(hexString,
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out i))
                    return negative ? (ushort)-i : i;
            }
            // decimal
            if (ushort.TryParse(s, out i))
                return negative ? (ushort)-i : i;
            
            // nonnumeric
            return null;
        }

        private bool DoOperandsMatch(LineInfo Line, Processor.Instruction Inst, int OperandIndex)
        {
            string opB = Inst.GetOperand(OperandIndex);

            if (opB.Length == 0 && Line.NumOperands > OperandIndex)
                return false;

            if (Line.NumOperands <= OperandIndex)
                return false;

            Operand opA = Line.Operands[OperandIndex];
            
            if (!opA.Exists)
                return opB.Length == 0;
            else if (opB.Length == 0)
                return false;
            
            if (!opA.IsIndirect && opA.Text == opB)
                return true;

            if (opA.IsIndexedRegister)
            {
                if (opA.IsIndirect)
                {
                    if (opB[0] != '(')
                        return false;
                    if (opA.Text.StartsWith("IX"))
                        return opB.StartsWith("(IX");
                    if (opA.Text.StartsWith("IY"))
                        return opB.StartsWith("(IY");
                    if (IsInParentheses(opB))
                        return opA.Text == RemoveParentheses(opB);
                }
                else
                {
                    if (opA.Text.StartsWith("IX"))
                        return opB.StartsWith("IX");
                    if (opA.Text.StartsWith("IY"))
                        return opB.StartsWith("IY");
                }
                return false;
            }
            else if (opA.IsIndirect)
            {
                if (opA.IsRegister && IsInParentheses(opB))
                    return opA.Text == RemoveParentheses(opB);
                if (opA.IsNumeric)
                    return opB == "(N)" || opB == "(NN)";
            }
            else if (opA.IsNumeric)
            {
                if (ArgImpliedInOpcode(Line.Mnemonic))
                    return opA.NumericValue == Lib.HexToUShort(opB);
                else
                    return opB == "N" || opB == "NN" || opB == "e";
            }
            
            if (IsRegister(opA.Text) || IsRegister(opB))
                return false;  // They would have matched if the same

            if (IsFlagState(opA.Text) || IsFlagState(opB))
                return false;

            // They must both be numeric
            return true;
        }

        private void SaveIntermediateFile()
        {
            intermediateFile = Path.Combine(Path.GetDirectoryName(originalFilePath),
                                            Path.GetFileNameWithoutExtension(originalFilePath)) + ".int.txt";

            Storage.SaveTextFile(intermediateFile,
                                 string.Join(Environment.NewLine,
                                             unit.Select(lp => string.Format("{0:00000} {1}", lp.SourceFileLine, lp.FullNameWithOriginalLineAsCommentWithErrorIfAny))) +
                                 Environment.NewLine +
                                 Environment.NewLine +
                                 SymbolTableToString());
        }
        private string SymbolTableToString()
        {
            return "SYMBOL TABLE" + Environment.NewLine +
                   "==================================" + Environment.NewLine +
                   String.Join(Environment.NewLine,
                               symbolTable.OrderBy(kv => kv.Key)
                                          .Select(kv => kv.Key.PadRight(15) +
                                                        kv.Value.Address.ToHexString() +
                                                        string.Format(" (Line {0}, {1})", kv.Value.SourceFileLine, kv.Value.Mnemonic)));
        }
        private bool LoadToBuffer(ref int LineNumber, byte[] Buffer, out ushort lowAddress, out ushort highAddress)
        {
            lowAddress  = 0xFFFF;
            highAddress = 0x0000;
            bool any = false;

            while (LineNumber < unit.Count)
            {
                var lp = unit[LineNumber++];

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
        private string SaveAsCMD(List<LineInfo> Unit)
        {
            string cmdFilePath = string.Empty;
            try
            {
                byte[] buffer = new byte[0x10000];

                var data = new List<(ushort SegmentAddress, byte[] Bytes)>();
                int lineNum = 0;
                ushort lowestAddress = 0xFFFF;

                string title = Path.GetFileNameWithoutExtension(originalFilePath);
                cmdFilePath = Path.Combine(Path.GetDirectoryName(originalFilePath), title + ".cmd");

                while (LoadToBuffer(ref lineNum, buffer, out ushort lowAddress, out ushort highAddress))
                {
                    var segment = new byte[highAddress - lowAddress + 1];
                    Array.Copy(buffer, lowAddress, segment, 0, highAddress - lowAddress);

                    data.Add((lowAddress, segment));
                    lowestAddress = Math.Min(lowAddress, lowestAddress);
                }
                SaveCMDFile(title, cmdFilePath, data, GetSymbolValue(symbolTable, null, "ENTRY") ?? execAddress ?? lowestAddress);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, "Failed to save CMD file from assembler.");
            }
            return cmdFilePath;
        }
        private static void SaveCMDFile(string Title, string FilePath, List<(ushort SegmentAddress, byte[] Bytes)> Data, ushort TransferAddress)
        {
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

            foreach (var d in Data)
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
            TransferAddress.Split(out lowDest, out highDest);
            writer.Write(lowDest);
            writer.Write(highDest);

            writer.Close();
        }
    }
}