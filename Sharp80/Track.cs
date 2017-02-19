using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal sealed class Track
    {
        private List<Sector> sectors;

        public Track(int TrackLength, byte TrackNumber, bool SideOne)
        {
            if (TrackLength > Floppy.MAX_TRACK_LENGTH)
                throw new Exception("Track too long.");

            this.TrackNumber = TrackNumber;
            this.SideOne = SideOne;
            Data = new byte[TrackLength];
            DoubleDensityArray = new bool[0];
            sectors = new List<Sector>();
        }

        public byte TrackNumber { get; private set; }
        public bool SideOne { get; private set; }
        public int Length
        { get { return Data.Length; } }
        public bool? DoubleDensity 
        { get; private set; }
        public byte SectorCount
        { get { return (byte)sectors.Count; } }
        public bool Formatted
        {
            get { return sectors.Count > 0; }
        }

        public void Deserialize(byte[] Data, bool[] DoubleDensityArray)
        {
            try
            {
                if (Data.Length > Floppy.MAX_TRACK_LENGTH)
                    throw new Exception("Track too long.");

                this.Data = Data;
                this.DoubleDensityArray = DoubleDensityArray;
                sectors.Clear();

                int physicalSectorIndex = 0;
                ushort crc = 0xFFFF;
                for (uint i = 0; i < Data.Length; i++)
                {
                    bool doubleDensity = DoubleDensityArray[physicalSectorIndex];

                    do
                    {
                        if (i >= Data.Length)
                            return;
                        if (!doubleDensity)
                            crc = 0xFFFF;
                    }
                    while (FetchByte(Data, ref i, ref crc, true, doubleDensity) != Floppy.IDAM);

                    uint idamLocation = i - 1;
                    byte trackNumber = FetchByte(Data, ref i, ref crc, false, doubleDensity);
                    bool sideOne = ((FetchByte(Data, ref i, ref crc, false, doubleDensity) & 0x01) == 0x01);
                    byte sectorNumber = FetchByte(Data, ref i, ref crc, false, doubleDensity);
                    uint sectorDataLength = Floppy.GetDataLengthFromCode((byte)(FetchByte(Data, ref i, ref crc, false, doubleDensity) & 0x03));

                    ushort actualAddressCRC = crc;

                    byte high = FetchByte(Data, ref i, ref crc, false, doubleDensity);
                    byte low = FetchByte(Data, ref i, ref crc, false, doubleDensity);

                    ushort recordedAddressCRC = Lib.CombineBytes(low, high);

                    int limit = doubleDensity ? 43 : 30;

                    do
                    {
                        if (!doubleDensity)
                            crc = Floppy.CRC_RESET;
                        else
                            crc = Floppy.CRC_RESET_A1_A1_A1;
                    }
                    while (!IsDAM(FetchByte(Data, ref i, ref crc, true, doubleDensity)) && limit-- >= 0);

                    if (limit < 0)
                    {
                        Log.LogMessage(string.Format("No DAM found: Track {0} Physical Sector {1}", this, physicalSectorIndex));
                        continue;
                    }
                    byte dam = Data[i - 1];

                    uint dataStart = i;
                    uint dataEnd = i + sectorDataLength;

                    for (int j = 0; j < sectorDataLength; j++)
                        FetchByte(Data, ref i, ref crc, false, doubleDensity);

                    ushort actualDataCRC = crc;
                    high = FetchByte(Data, ref i, ref crc, false, doubleDensity);
                    low = FetchByte(Data, ref i, ref crc, false, doubleDensity);
                    ushort recordedDataCRC = Lib.CombineBytes(low, high);

                    Sector s = new Sector(trackNumber,
                                          sideOne,
                                          sectorNumber,
                                          idamLocation,
                                          doubleDensity,
                                          dam,
                                          dataStart,
                                          dataEnd,
                                          (actualAddressCRC != recordedAddressCRC),
                                          (actualDataCRC != recordedDataCRC));

                    sectors.Add(s);

                    if (s.AddressCRCError)
                        Log.LogMessage(string.Format("Address CRC Error: {0} ", s));

                    if (s.DataCRCError)
                        Log.LogMessage(string.Format("Data CRC Error: {0} ", s));

                    physicalSectorIndex++;

                }
            }
            finally
            {
                if (sectors.Count > 0)
                {
                    if (sectors.All(s => s.DoubleDensity))
                        this.DoubleDensity = true;
                    else if (sectors.All(s => !s.DoubleDensity))
                        this.DoubleDensity = false;
                    else
                        this.DoubleDensity = null;

                    uint lastByte = sectors.Max(s => s.DataEndIndex);
                    lastByte += 2   // CRC
                              + 54  // Typical Sector End Filler
                             + 598; // typical Track end filler

                    int i;
                    byte filler = DoubleDensity == true ? Floppy.FILLER_BYTE_DD : Floppy.FILLER_BYTE_SD;
                    for (i = Data.Length - 1; i > lastByte; i--)
                        if (this.Data[i] != filler)
                            break;

                    if (Data.Length > i + 100)
                    {
                        var temp = Data;
                        Data = new byte[i];
                        Array.Copy(temp, Data, i);
                    }
                }
                else
                {
                    this.DoubleDensity = null;
                }
                System.Diagnostics.Debug.WriteLine(string.Format("Loaded Track: {0} Sector Order: ({1})", this.TrackNumber, string.Join(",", this.sectors.Select(s => s.SectorNumber.ToString()))));
            }
        }
        public byte[] GetSectorData(byte SectorNumber)
        {
            Sector s = sectors.FirstOrDefault(ss => ss.SectorNumber == SectorNumber);

            if (s == null)
                return new byte[0];

            byte[] data = new byte[s.DataEndIndex - s.DataStartIndex];

            Array.Copy(this.Data, s.DataStartIndex, data, 0, data.Length);

            return data;
        }
        public byte GetDAM(byte SectorNumber)
        {
            Sector s = sectors.FirstOrDefault(ss => ss.SectorNumber == SectorNumber);

            if (s == null)
                return 0;
            else
                return s.DAM;
        }
        internal SectorDescriptor GetSector(byte SectorIndex)
        {
            if (sectors.Count == 0)
                return null;
            else if (SectorIndex < 0)
                SectorIndex = 0;
            else if (SectorIndex >= sectors.Count)
                SectorIndex = (byte)(sectors.Count - 1);

            return sectors.OrderBy(s => s.SectorNumber).Skip(SectorIndex).First()?.ToSectorDescriptor(Data);
        }
        public bool IsDoubleDensity(byte SectorNumber)
        {
            Sector s = sectors.FirstOrDefault(ss => ss.SectorNumber == SectorNumber);

            if (s == null)
                return false;
            else
                return s.DoubleDensity;
        }
        public bool HasIDAMAt(uint Index, out bool DoubleDensity)
        {
            Sector s = sectors.FirstOrDefault(ss => ss.IdamLocation == Index);

            if (s == null)
            {
                DoubleDensity = false;
                return false;
            }
            else
            {
                DoubleDensity = s.DoubleDensity;
                return true;
            }
        }
        
        public byte[] Data
        { get; private set; }
        public bool[] DoubleDensityArray
        { get; private set; }

        public List<SectorDescriptor> ToSectorDescriptors()
        {
            return sectors.Select(s => s.ToSectorDescriptor(this.Data)).ToList();
        }
        private static bool IsDAM(byte Byte)
        {
            return (Byte >= 0xF8) && (Byte <= 0xFB);
        }
        private static byte FetchByte(byte[] Data, ref uint i, ref ushort crc, bool AllowResetCRC, bool DoubleDensity)
        {
            byte b = 0;
            if (i < Data.Length)
            {
                b = Data[i];
                crc = FloppyController.UpdateCRC(crc, b, AllowResetCRC, DoubleDensity);
            }
            i++;
            return b;
        }
        public override string ToString()
        {
            return string.Format("Track {0} Side {1}", this.TrackNumber, this.SideOne ? 1 : 0);
        }
        private class Sector
        {
            public Sector(byte TrackNumber, bool SideOne, byte SectorNumber, uint IdamLocation, bool DoubleDensity, byte DAM, uint DataStartIndex, uint DataEndIndex, bool AddressCRCError, bool DataCRCError)
            {
                this.TrackNumber = TrackNumber;
                this.SideOne = SideOne;
                this.SectorNumber = SectorNumber;
                this.IdamLocation = IdamLocation;
                this.DoubleDensity = DoubleDensity;
                this.DAM = DAM;
                this.DataStartIndex = DataStartIndex;
                this.DataEndIndex = DataEndIndex;
                this.AddressCRCError = AddressCRCError;
                this.DataCRCError = DataCRCError;
            }

            public SectorDescriptor ToSectorDescriptor(byte[] TrackData)
            {
                SectorDescriptor sd = new SectorDescriptor(this.TrackNumber, this.SectorNumber);
                sd.SideOne = this.SideOne;
                sd.CrcError = this.AddressCRCError || this.DataCRCError;
                sd.DAM = this.DAM;
                sd.DoubleDensity = this.DoubleDensity;
                sd.InUse = true;
                sd.NonIbm = false;
                sd.SectorSize = (ushort)(this.DataEndIndex - this.DataStartIndex);
                sd.SectorSizeCode = Floppy.GetDataLengthCode(sd.SectorSize);
                sd.SectorData = new byte[sd.SectorSize];
                Array.Copy(TrackData, this.DataStartIndex, sd.SectorData, 0, sd.SectorSize);

                return sd;
            }

            public byte TrackNumber { get; private set; }
            public bool SideOne { get; private set; }
            public byte SectorNumber { get; private set; }
            public bool DoubleDensity { get; private set; }
            public byte DAM { get; private set; }
            public uint IdamLocation { get; private set; }
            public uint DataStartIndex { get; private set; }
            public uint DataEndIndex { get; private set; }
            public bool AddressCRCError { get; private set; }
            public bool DataCRCError { get; private set; }

            public override string ToString()
            {
                return string.Format("Side: {0} Trk: {1} Sec: {2} {3} CRC: {4}",
                                     SideOne ? 1 : 0,
                                     TrackNumber,
                                     SectorNumber,
                                     DoubleDensity ? "DDen" : "SDen",
                                     (AddressCRCError || DataCRCError) ? "Bad" : "OK");
            }

            public bool Compare(Sector other, byte[] ThisData, byte[] OtherData)
            {
                bool same = other.TrackNumber == this.TrackNumber &&
                         (other.SideOne == this.SideOne) &&
                          other.SectorNumber == this.SectorNumber;

                if (!same)
                    return false;

                uint j = other.DataStartIndex;
                for (uint i = this.DataStartIndex; i < this.DataEndIndex; i++)
                    if (ThisData[i] != OtherData[j])
                        return false;

                return true;
            }
        }
    }
}
