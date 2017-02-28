//#define DEBUGLOG

/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharp80
{
    internal static class Log
    {
        private static List<string> log = new List<string>();

        public static bool TraceOn { get; set; } = false;
        
        public static void LogTrace(string Message)
        {
            if (TraceOn)
            {
                var msg = DateTime.Now.ToString("hh:mm:ss.ffffff") + ": " + Message;
                log.Add(msg);
            }
        }
        [Conditional("DEBUGLOG")]
        public static void LogDebug(string Message)
        {
            var msg = DateTime.Now.ToString("hh:mm:ss.ffffff") + ": " + Message;
            log.Add(msg);
        }

        public static void LogException(Exception ex)
        {
            LogDebug(ex.ToString());
        }

        public static void Save()
        {
            Storage.SaveTextFile(System.IO.Path.Combine(Lib.GetAppPath(), "trace.txt"), log);
            log.Clear();
        }
    }
}
