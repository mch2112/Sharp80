/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Sharp80
{
    internal static class ExceptionHandler
    {
        private static bool terminating = false;

        /// <summary>
        /// We save exception events in a queue that can be processed by the main form's ui thread,
        /// because showing dialogs can only be done in that thread.
        /// </summary>
        private static Queue<(Exception Exception, ExceptionHandlingOptions Option, string Message)> ExceptionQueue = new Queue<(Exception Exception, ExceptionHandlingOptions Option, string Message)>();

        public static void Handle(Exception Ex, ExceptionHandlingOptions Option = ExceptionHandlingOptions.Terminate, string Message = "")
        {
            if (Message.Length > 0)
                Log.LogDebug(Message + Environment.NewLine + Ex.ToReport());
            else
                Log.LogDebug(Ex.ToReport());

            if (!terminating && Option != ExceptionHandlingOptions.LogOnly)
            {
                ExceptionQueue.Enqueue((Ex, Option, Message));
                if (Option == ExceptionHandlingOptions.Terminate)
                    terminating = true;
            }
            HandleExceptions();
        }
        /// <summary>
        /// Only works if called from UI thread
        /// </summary>
        public static void HandleExceptions()
        {
            if (MainForm.IsUiThread)
            {
                while (ExceptionQueue.Count > 0)
                {
                    var q = ExceptionQueue.Dequeue();

                    if (q.Message.Length > 0)
                    {
                        Dialogs.AlertUser(q.Message);
                    }
                    else
                    {
                        if (Dialogs.AskYesNo("An error has been detected in your application. Please click Yes to copy the details to your Windows clipboard." +
                            Environment.NewLine +
                            Environment.NewLine +
                            "Please copy and email these results to mchamilton2112@gmail.com for followup."))
                        {
                            Clipboard.SetText(q.Exception.ToReport());
                            if (q.Option == ExceptionHandlingOptions.Terminate)
                                Application.Exit();
                        }
                    }
                }
            }
        }
    }
}
