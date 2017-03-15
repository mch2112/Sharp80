//#define DEBUGLOG
#undef DEBUGLOG

/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Sharp80
{
    internal enum ExceptionHandlingOptions { LogOnly, InformUser, Terminate }
    internal static class Log
    {
        public delegate ulong GetTickDelegate();

        /// <summary>
        /// We save exception events in a queue that can be processed by the main form's ui thread,
        /// because showing dialogs can only be done in that thread.
        /// </summary>
        public static Queue<Tuple<Exception, ExceptionHandlingOptions>> ExceptionQueue = new Queue<Tuple<Exception, ExceptionHandlingOptions>>();

        public static bool TraceOn { get; set; } = false;

        private const int MAX_LOG_ITEMS = 1000000;

        private static List<Tuple<ulong, string>> log = new List<Tuple<ulong, string>>();
        private static GetTickDelegate tickFn = () => 0;
        private static bool terminating = false;

        public static void Initalize(GetTickDelegate Callback)
        {
            tickFn = Callback;
        }
        public static void LogTrace(string Message)
        {
            if (TraceOn)
            {
                LogItem(Message);
            }
        }

        [Conditional("DEBUGLOG")]
        public static void LogDebug(string Message)
        {
            LogItem(Message);
        }
        public static void LogException(Exception Ex, ExceptionHandlingOptions Option = ExceptionHandlingOptions.Terminate)
        {
            LogDebug(Ex.ToReport());
            if (!terminating && Option != ExceptionHandlingOptions.LogOnly)
            {
                ExceptionQueue.Enqueue(new Tuple<Exception, ExceptionHandlingOptions>(Ex, Option));
                if (Option == ExceptionHandlingOptions.Terminate)
                    terminating = true;
            }
        }
        public static bool Save(bool Flush, out string Path)
        {
            if (log.Count > 0)
            {
                Path = System.IO.Path.Combine(Storage.AppDataPath, "trace.txt");

                // create a new log so that we can save the old one without
                // it being modified.
                var oldLog = log;
                log = new List<Tuple<ulong, string>>();

                Storage.SaveTextFile(Path, oldLog.Select(l => string.Format("{0:000,000,000,000}: {1}", l.Item1, l.Item2)));

                // and restore if not flushed
                if (!Flush)
                {
                    var newLog = log;
                    log = oldLog;
                    log.AddRange(newLog);
                }
                return true;
            }   
            else
            {
                Path = String.Empty;
                return false;
            }
        }
        private static void LogItem(string Message)
        {
            if (log.Count >= MAX_LOG_ITEMS)
                log.RemoveRange(0, MAX_LOG_ITEMS / 10);

            log.Add(new Tuple<ulong, string>(tickFn(), Message));
        }
    }
}
