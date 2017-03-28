/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace Sharp80
{
    internal class IO
    {
        // BYTE ARRAYS

        internal static bool LoadBinaryFile(string FilePath, out byte[] Bytes)
        {
            Bytes = null;
            try
            {
                if (!File.Exists(FilePath))
                    return false;

                Bytes = File.ReadAllBytes(FilePath);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" already is use.");
                else if (ex is FileNotFoundException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" not found.");
                else
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser);

                Bytes = null;
                return false;
            }
        }
        internal static bool SaveBinaryFile(string FilePath, byte[] Data)
        {
            try
            {
                File.WriteAllBytes(FilePath, Data);
                return true;
            }
            catch (Exception ex)
            {
                ex.Data["ExtraMessage"] = $"Exception saving file {FilePath}";

                if (ex is IOException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" already is use.");
                else if (ex is FileNotFoundException)
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser, $"File \"{Path.GetFileName(FilePath)}\" not found.");
                else
                    ExceptionHandler.Handle(ex, ExceptionHandlingOptions.InformUser);

                return false;
            }
        }

        // TEXT

        internal static bool LoadTextFile(string FilePath, out string Text)
        {
            try
            {
                Text = File.ReadAllText(FilePath);
                return true;
            }
            catch
            {
                Text = null;
                return false;
            }
        }
        internal static void SaveTextFile(string FilePath, IEnumerable<string> Lines) => File.WriteAllLines(FilePath, Lines);
        internal static void SaveTextFile(string FilePath, string Text) =>  File.WriteAllText(FilePath, Text);
    }
}
