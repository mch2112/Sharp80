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
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                Text = null;
                return false;
            }
        }
    }
}
