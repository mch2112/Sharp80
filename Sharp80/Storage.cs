using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sharp80
{
    internal static class Storage
    {
        public static byte[] LoadBinaryFile(string FilePath)
        {
            return File.ReadAllBytes(FilePath);
        }
        public static List<string> LoadTextFile(string FilePath)
        {
            return new List<string>(File.ReadAllLines(FilePath));
        }
        public static bool SaveBinaryFile(string FilePath, byte[] Data)
        {
            try
            {
                File.WriteAllBytes(FilePath, Data);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogMessage(string.Format("Exception saving file {0}: {1}", FilePath, ex));
                return false;
            }
        }
        public static void SaveTextFile(string FilePath, IEnumerable<string> Lines)
        {
            File.WriteAllLines(FilePath, Lines);
        }
        public static void SaveTextFile(string FilePath, string Text)
        {
            File.WriteAllText(FilePath, Text);
        }
        public static string GetDefaultDriveFileName(byte DriveNum)
        {
            string fileName = String.Empty;

            switch (DriveNum)
            {
                case 0:
                    fileName = Settings.Disk0Filename;
                    break;
                case 1:
                    fileName = Settings.Disk1Filename;
                    break;
                case 2:
                    fileName = Settings.Disk2Filename;
                    break;
                case 3:
                    fileName = Settings.Disk3Filename;
                    break;
            }
            if (File.Exists(fileName))
                return fileName;
            else
                return String.Empty;
        }
        public static void SaveDefaultDriveFileName(byte DriveNum, string FilePath)
        {
            switch (DriveNum)
            {
                case 0:
                    Settings.Disk0Filename = FilePath;
                    break;
                case 1:
                    Settings.Disk1Filename = FilePath;
                    break;
                case 2:
                    Settings.Disk2Filename = FilePath;
                    break;
                case 3:
                    Settings.Disk3Filename = FilePath;
                    break;
            }
        }
        public static Floppy MakeBlankFloppy(bool Formatted)
        {
            return DMK.MakeBlankFloppy(NumTracks: 40,
                                           DoubleSided: false,
                                           Formatted: Formatted);
        }
        public static void SaveCMDFile(string Title, string FilePath, ushort[] Origin, byte[][] Data, ushort TransferAddress)
        {
            BinaryWriter writer = new BinaryWriter(File.Open(FilePath, FileMode.Create));

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

            for (int i = 0; i < Origin.Length; i++)
            {
                dest = Origin[i];
                cursor = 0;

                segmentSize = Data[i].Length;

                while (cursor < segmentSize)
                {
                    blockSize = Math.Min(0x100, Data[i].Length - cursor);
                    writer.Write((byte)0x01);   // block marker
                    writer.Write((byte)(blockSize + 2)); // 0x02 == 256 bytes
                    Lib.SplitBytes(dest, out lowDest, out highDest);
                    writer.Write(lowDest);
                    writer.Write(highDest);
                    while (blockSize-- > 0)
                    {
                        writer.Write(Data[i][cursor++]);
                        dest++;
                    }
                }
            }
            writer.Write((byte)0x02);  // transfer address marker
            writer.Write((byte)0x02);  // transfer address length
            Lib.SplitBytes(TransferAddress, out lowDest, out highDest);
            writer.Write(lowDest);
            writer.Write(highDest);

            writer.Close();
        }
        public static ushort? LoadCMDFile(string FilePath, IMemory mem)
        {
            // Returns the address to start executing

            byte code;
            int length;
            byte[] data = new byte[0x101];

            ushort destAddress;
            ushort? execAddress = null;

            if (File.Exists(FilePath))
            {
                try
                {
                    byte[] b = Storage.LoadBinaryFile(FilePath);

                    int i = 0;
                    while (i < b.Length)
                    {
                        code = b[i++];
                        length = b[i++];

                        if (length == 0)
                            length = 0x100;

                        Array.Copy(b, i, data, 0, length);
                        i += length;

                        switch (code)
                        {
                            case 0x00:
                                // do nothing
                                break;
                            case 0x01:          // object code (load block)
                                switch (length)
                                {
                                    case 1:
                                        destAddress = Lib.CombineBytes(data[0], b[i++]);
                                        for (int k = 0; k < 0xFF; k++)
                                            mem[destAddress++] = b[i++];
                                        break;
                                    case 2:
                                        destAddress = Lib.CombineBytes(data[0], data[1]);
                                        for (int k = 0; k < 0x100; k++)
                                            mem[destAddress++] = b[i++];
                                        break;
                                    default:
                                        destAddress = Lib.CombineBytes(data[0], data[1]);
                                        for (int k = 2; k < length; k++)
                                            mem[destAddress++] = data[k];
                                        break;
                                }
                                Debug.Assert(length != 0x00);
                                break;

                            case 0x02:          // transfer address
                                if (length == 0x01)
                                    execAddress = data[0];
                                else if (length == 0x02)
                                    execAddress = Lib.CombineBytes(data[0], data[1]);
                                else
                                    throw new Exception("CMD file Error.");
                                break;
                            case 0x03:
                                // Do nothing (non executable marker)
                                break;
                            case 0x04:          // end of partitioned data set member
                                // Do nothing
                                break;
                            case 0x05:          // load module header
                                // Do nothing
                                break;
                            case 0x06:          // partitioned data set header
                                // Do nothing
                                break;
                            case 0x07:          // patch name header
                                // Do nothing
                                break;
                            case 0x08:          // ISAM directory entry
                                // Do nothing
                                break;
                            case 0x0A:          // end of ISAM directory
                                // Do nothing
                                break;
                            case 0x0C:          // PDS directory entry
                                // Do nothing
                                break;
                            case 0x0E:          // end of PDS directory
                                // Do nothing
                                break;
                            case 0x10:          // yanked load block
                                // Do nothing
                                break;
                            case 0x1F:          // copyright block
                                // Do nothing
                                break;
                            default:
                                //throw new Exception("Error in CMD file.");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogException(ex);
                    return null;
                }
            }
            else
            {
                return null;
            }
            return execAddress;
        }
    }
}

