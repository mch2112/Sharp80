using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal static class Log
    {
        static Log() { DebugOn = true; UnifyTraceAndDebug = true; }

        private static List<string> log = new List<string>(100);
        private static List<string> debugLog = new List<string>(10000);
        private static List<string> traceLog = new List<string>();

        public static bool DebugOn { get; set; }
        public static bool TraceOn { get; set; }
        public static bool UnifyTraceAndDebug { get; set; }

        [Conditional("DEBUG")] 
        public static void LogToTrace(string Message)
        {
            if (TraceOn)
            {
                traceLog.Add(Message);
            }
        }

        [Conditional("DEBUG")] 
        public static void LogToDebug(string Message)
        {
            if (DebugOn && TraceOn)
            {
                string m = DateTime.Now.ToString("hh:mm:ss.ffffff") + ": " + Message;
                System.Diagnostics.Debug.WriteLine(m);
                
                debugLog.Add(m);

                if (UnifyTraceAndDebug)
                    LogToTrace(m);
            }
        }

        public static void LogMessage(string Message)
        {
            string m = DateTime.Now.ToString("hh:mm:ss.ffffff") + ": " + Message;
            log.Add(m);
#if DEBUG
            System.Diagnostics.Debug.WriteLine(m);

            if (DebugOn)
                LogToDebug(m);
#endif
        }
        public static void LogException(Exception ex)
        {
            LogMessage(ex.ToString());
        }
        public static void Purge()
        {
            debugLog.Clear();
            log.Clear();
        }
        public static void SaveLog()
        {
            Storage.SaveTextFile(System.IO.Path.Combine(Lib.GetAppPath(), "log.txt"), log);
        }

        [Conditional("DEBUG")] 
        public static void SaveTrace()
        {
            Storage.SaveTextFile(System.IO.Path.Combine(Lib.GetAppPath(), "trace.txt"), traceLog);
            traceLog.Clear();
        }
    }
}
