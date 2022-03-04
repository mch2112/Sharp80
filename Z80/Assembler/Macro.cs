/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharp80.Z80.Assembler
{
    public partial class Assembler
    {
        private class Macro
        {
            public string Name { get; private set; }

            private List<string> arguments;
            private List<string> lines = new List<string>();

            public Macro(string line)
            {
                Name = GetCol(line, 0).Replace(":", String.Empty);
                arguments = new List<string>(GetCSV(GetCol(line, 2), 10000));

                Debug.Assert(GetCol(line, 1) == "MACRO");
                for (int i = 0; i < arguments.Count; i++)
                    Debug.Assert(arguments[i] == arguments[i].ToUpper());
            }
            public void AddLine(string Line) => lines.Add(Line);
            public List<string> Expand(string inputArguments, int InputLineNumber, out string Error)
            {
                Error = String.Empty;

                string line;
                List<string> returnLines = new List<string>();
                int argNum;
                string[] inputArgs = GetCSV(inputArguments, 1000);

                if (inputArgs.Length != arguments.Count)
                    Error = string.Format($"Macro {Name} Arguments Mismatch: {arguments.Count} Required, {inputArgs.Length} Specified, Line {InputLineNumber}");

                foreach (string l in lines)
                {
                    argNum = 0;
                    line = l;
                    foreach (string arg in arguments)
                    {
                        if (argNum < inputArgs.Length)
                            line = line.Replace("&" + arg, inputArgs[argNum++]);
                        else
                            line = line.Replace("&" + arg, String.Empty);
                    }
                    returnLines.Add(line);
                }
                return returnLines;
            }
        }
    }
}
