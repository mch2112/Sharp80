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
    internal static class Log
    {
        public delegate ulong GetTickDelegate();

        private const int MAX_LOG_ITEMS = 1000000;

        private static List<Tuple<ulong, string>> log = new List<Tuple<ulong, string>>();

        private static GetTickDelegate tickFn = () => 0;
        public static bool TraceOn { get; set; } = false;

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
        public static void LogException(Exception ex, bool AllowContinue = false)
        {
            LogDebug(ex.ToReport());
            if (!terminating)
            {
                if (Dialogs.AskYesNo("An error has been detected in your application. Please click Yes to copy the details to your Windows clipboard." +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Please copy and email these results to mchamilton2112@gmail.com for followup."))
                {
                    System.Windows.Forms.Clipboard.SetText(ex.ToReport());
                    if (!AllowContinue)
                    {
                        terminating = true;
                        System.Windows.Forms.Application.Exit();
                    }
                }
            }
        }
        public static bool Save(bool Flush, out string Path)
        {
            if (log.Count > 0)
            {
                Path = System.IO.Path.Combine(Storage.AppDataPath, "trace.txt");
                Storage.SaveTextFile(Path, log.Select(l => string.Format("{0:000,000,000,000}: {1}", l.Item1, l.Item2)));
                if (Flush)
                    log.Clear();
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
