/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharp80
{
    internal static class Log
    {
        private static List<string> traceLog = new List<string>();

        public static bool TraceOn { get; set; }
        public static bool Available
        {
            get
            {
                return
#if DEBUG
                    true;
#else
                false;
#endif
            }
        }

        [Conditional("DEBUG")] 
        public static void LogTrace(string Message)
        {
            if (TraceOn)
            {
                Log(Message);
            }
        }
        [Conditional("DEBUG")]
        public static void Log(string Message)
        {
            var msg = DateTime.Now.ToString("hh:mm:ss.ffffff") + ": " + Message;
            traceLog.Add(msg);
        }

        [Conditional("DEBUG")]
        public static void LogException(Exception ex)
        {
            Log(ex.ToString());
        }

        [Conditional("DEBUG")] 
        public static void Save()
        {
            Storage.SaveTextFile(System.IO.Path.Combine(Lib.GetAppPath(), "trace.txt"), traceLog);
            traceLog.Clear();
        }
    }
}
