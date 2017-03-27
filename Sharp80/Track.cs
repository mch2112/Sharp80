/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharp80
{
    internal class Track
    {
        public byte PhysicalTrackNum { get; }
        public bool SideOne { get; }
        public int LengthWithHeader { get; }
        public bool Changed { get; private set; } = false;

        private byte[] Data { get; set; }
        private ushort[] header;
        private bool? density = null;
        private bool[] densityMap = null;

        public const int MAX_LENGTH_WITH_HEADER = 0x2940;
        public const int DEFAULT_LENGTH_WITH_HEADER = 0x1900;
        private const int HEADER_LENGTH_BYTES = 0x80;
        public const int DEFAULT_LENGTH_WITHOUT_HEADER = 0x1900 - HEADER_LENGTH_BYTES;
        private const int HEADER_LENGTH = 0x40;
        private const ushort DOUBLE_DENSITY_MASK = 0x8000;
        private const ushort OFFSET_MASK = 0x3FFF;
        private const ushort HEADER_MASK = DOUBLE_DENSITY_MASK | OFFSET_MASK;

        private List<SectorDescriptor> sectorDescriptorCache = null;

        private static Random r = new Random();

        public Track(byte PhysicalTrackNum, bool SideOne, byte[] Data, bool SingleDensitySingleByte)
        {
            this.PhysicalTrackNum = PhysicalTrackNum;
            this.SideOne = SideOne;

            // Combine to single ushort, strip out unused bit 14
            header = Data.Slice(0, HEADER_LENGTH_BYTES).ToUShortArray().Select(h => (ushort)(h & (OFFSET_MASK | DOUBLE_DENSITY_MASK))).ToArray();

            this.Data = Data.Slice(HEADER_LENGTH_BYTES);
            SetDensity();

            if (SingleDensitySingleByte)
                ConvertFromSingleByte();

            LengthWithHeader = this.Data.Length + HEADER_LENGTH_BYTES;
            densityMap = null;
        }

        private void SetDensity()
        {
            if (header.All(h => h >= DOUBLE_DENSITY_MASK || h == 0))
                density = true;
            else if (header.All(h => h < DOUBLE_DENSITY_MASK))
                density = false;
            else
                density = null;
        }
        public static byte[] ToTrackBytes(IEnumerable<SectorDescriptor> Sectors, int Length = 0)
        {
            byte[] ret = new byte[MAX_LENGTH_WITH_HEADER * 2]; // big

            int i = 0;
            ret.SetValues(ref i, HEADER_LENGTH_BYTES, (byte)0x00);

            bool ddAll = Sectors.All(s => s.DoubleDensity);

            int headerCursor = 0;

            // In some places we use fewer bytes than the 1773 tech reference states so that we can fit
            // 18 DD sectors (or 10 SD sectors) in a standard length track. Not sure why WD made it
            // so the standard doesn't work.

            if (ddAll)
            {
                ret.SetValues(ref i, 10, (byte)0x4E)  /* standard is 80 */
                   .SetValues(ref i, 12, (byte)0x00)
                   .SetValues(ref i,  3, (byte)0xF6)
                   .SetValues(ref i,  1, (byte)0xFC) 
                   .SetValues(ref i, 50, (byte)0x4E); /* standard is 50 */
            }
            else
            {
                // times two for doubled bytes
                ret.SetValues(ref i, 10 * 2, (byte)0xFF)  /* tech datasheet says 30, not 10 */
                   .SetValues(ref i,  6 * 2, (byte)0x00)
                   .SetValues(ref i,  1 * 2, (byte)0xFC)
                   .SetValues(ref i, 26 * 2, (byte)0xFF);
            }
            byte sideNum;
            byte dataLengthCode;
            byte b;
            foreach (var s in Sectors)
            {
                sideNum = (byte)(s.SideOne ? 1 : 0);
                dataLengthCode = Floppy.GetDataLengthCode(s.SectorData.Length);
                ushort crc;
                if (s.DoubleDensity)
                {
                    crc = Floppy.CRC_RESET_A1_A1_A1_FE;

                    ret.SetValues(ref i, 12, (byte)0x00)
                       .SetValues(ref i, 3, (byte)0xA1);

                    ((ushort)(i + DOUBLE_DENSITY_MASK)).Split(out ret[headerCursor], out ret[headerCursor + 1]);
                }
                else
                {
                    crc = Floppy.CRC_RESET_FE;
                    ret.SetValues(ref i, 6 * 2, (byte)0x00);

                    ((ushort)i).Split(out ret[headerCursor], out ret[headerCursor + 1]);
                }
                headerCursor += 2;
                ret.SetValues(ref i, !s.DoubleDensity, Floppy.IDAM, s.TrackNumber, sideNum, s.SectorNumber, dataLengthCode);

                crc = Lib.Crc(crc, s.TrackNumber, sideNum, s.SectorNumber, dataLengthCode);
                crc.Split(out byte crcLow, out byte crcHigh);

                if (s.DoubleDensity)
                {
                    ret.SetValues(ref i, false, crcHigh, crcLow)
                       .SetValues(ref i, 22, (byte)0x4E)
                       .SetValues(ref i, 12, (byte)0x00)
                       .SetValues(ref i, false, (byte)0xA1, (byte)0xA1, (byte)0xA1, s.DAM);

                    crc = Lib.Crc(Floppy.CRC_RESET_A1_A1_A1, s.DAM);
                    for (int j = 0; j < s.SectorData.Length; j++)
                    {
                        b = s.SectorData[j];
                        ret[i++] = b;
                        crc = Lib.Crc(crc, b);
                    }

                    if (s.CrcError)
                        crc = (ushort)~crc; // trash the crc

                    crc.Split(out crcLow, out crcHigh);

                    ret.SetValues(ref i, false, crcHigh, crcLow)
                       .SetValues(ref i, 20, (byte)0x4E); /* standard is 54 */
                }
                else // single density
                {
                    ret.SetValues(ref i, true, crcHigh, crcLow)
                       .SetValues(ref i, 11 * 2, (byte)0xFF)
                       .SetValues(ref i, 6 * 2, (byte)0x00)
                       .SetValues(ref i, 1 * 2, s.DAM);

                    crc = Floppy.CRC_RESET;
                    crc = Lib.Crc(crc, s.DAM);
                    for (int j = 0; j < s.SectorData.Length; j++)
                    {
                        b = s.SectorData[j];
                        ret[i++] = b;
                        ret[i++] = b;
                        crc = Lib.Crc(crc, b);
                    }

                    if (s.CrcError)
                        crc = (ushort)~crc; // trash the crc

                    crc.Split(out crcLow, out crcHigh);
                    ret.SetValues(ref i,  1 * 2, crcHigh)
                       .SetValues(ref i,  1 * 2, crcLow)
                       .SetValues(ref i, 17 * 2, (byte)0xFF); /* spec says 27, not 17 */
                }
            }
           
            if (Length > 0)
                ret = ret.Pad(Length, ddAll ? Floppy.FILLER_BYTE_DD : Floppy.FILLER_BYTE_SD).Slice(0, Length);

            else if (i <= 0x0CC0)
                ret = ret.Pad(0x0CC0, Floppy.FILLER_BYTE_SD);
            else
                ret = ret.Pad(DEFAULT_LENGTH_WITH_HEADER, ddAll ? Floppy.FILLER_BYTE_DD : Floppy.FILLER_BYTE_SD).Slice(0, MAX_LENGTH_WITH_HEADER);

            return ret;
        }
        private ushort[] Header
        {
            get
            {
                if (header == null)
                    RebuildHeader();
                return header;
            }
        }
        private bool GetDensity(int Index)
        {
            if (density.HasValue)
                return density.Value;

            if (densityMap == null)
                RebuildDensity();

            return densityMap[Index];
        }
        private void SetDensity(int Index, bool Value)
        {
            if (density != Value)
            {
                if (densityMap == null)
                    RebuildDensity();
                densityMap[Index] = Value;
            }
        }

        public int DataLength => Data.Length;

        public byte[] Serialize()
        {
            return Header.ToByteArray().Concat(Data);
        }
        public byte ReadByte(int TrackIndex, bool? DoubleDensity)
        {
            if (DoubleDensity.HasValue && DoubleDensity != GetDensity(TrackIndex))
                return (byte)r.Next(0, 0x100);

            if (DoubleDensity == false)
                TrackIndex &= 0x7FFFFFFE;

            if (TrackIndex >= Data.Length)
                TrackIndex = 0;

            var b = Data[TrackIndex];
#if DEBUG
            if (DoubleDensity.HasValue && !DoubleDensity.Value)
                if (Data[TrackIndex + 1] != b &&
                    Data[Math.Max(0, TrackIndex - 1)] != b)
                    throw new Exception("Density Inconsistency");
#endif
            return b;
        }
        public void WriteByte(int TrackIndex, bool DoubleDensity, byte Value)
        {
            header = null;
            Changed = true;

            if (DoubleDensity)
            {
                Data[TrackIndex] = Value;
                if (density != true)
                    SetDensity(TrackIndex, true);
            }
            else
            {
                TrackIndex &= 0xFFFFFFE;
                Data[TrackIndex] = Value;
                Data[TrackIndex + 1] = Value;
                if (density != false)
                {
                    SetDensity(TrackIndex, false);
                    SetDensity(TrackIndex + 1, false);
                }
            }
            sectorDescriptorCache = null;
        }
        public bool HasIdamAt(int TrackIndex, bool DoubleDensity)
        {
            if (Header == null)
                RebuildHeader();

            ushort target = (ushort)(TrackIndex + HEADER_LENGTH_BYTES + (DoubleDensity ? DOUBLE_DENSITY_MASK : 0));

            return Header.Any(us => (us & HEADER_MASK) == target);
        }
        public SectorDescriptor GetSectorDescriptor(byte SectorIndex)
        {
            sectorDescriptorCache = sectorDescriptorCache ?? GetSectorDescriptorCache();

            if (SectorIndex < sectorDescriptorCache.Count)
                return sectorDescriptorCache[SectorIndex];
            else
                return SectorDescriptor.Empty;
        }
        private List<SectorDescriptor> GetSectorDescriptorCache()
        {
            var sds = new List<SectorDescriptor>();

            for (int SectorIndex = 0; SectorIndex < HEADER_LENGTH && Header[SectorIndex] > 0; SectorIndex++)
            {
                bool density = Header[SectorIndex] >= DOUBLE_DENSITY_MASK;
                int offset = (Header[SectorIndex] & OFFSET_MASK) - HEADER_LENGTH_BYTES;
                int byteMultiple = density ? 1 : 2;
                if (Data[offset] != Floppy.IDAM)
                {
                    Log.LogDebug(string.Format($"Missing IDAM Track {PhysicalTrackNum} Side {(SideOne ? 1 : 0)} SectorIndex {SectorIndex}."));
                    continue;
                }

                byte dam = 0x00;
                int dataStart = 0x00;
                bool deleted = false;
                for (int i = offset + 7 * byteMultiple; i < offset + (7 + (density ? 43 : 30)) * byteMultiple; i += byteMultiple)
                {
                    if (FloppyController.IsDAM(Data[i], out deleted))
                    {
                        dam = Data[i];
                        dataStart = i + byteMultiple;
                        break;
                    }
                }

                var sizeCode = Data[offset + 4 * byteMultiple];
                var dataLength = Floppy.GetDataLengthFromCode(sizeCode);

                var sd = new SectorDescriptor()
                {
                    TrackNumber =    Data[offset + 1 * byteMultiple],
                    SideOne =        Data[offset + 2 * byteMultiple] > 0,
                    SectorNumber =   Data[offset + 3 * byteMultiple],
                    SectorSizeCode = sizeCode,
                    SectorSize =     dataLength,
                    DoubleDensity =  density,
                    SectorData =     new byte[dataLength]
                };

                if (dam == 0x00)
                {
                    sd.CrcError = true;
                    sd.InUse = false;
                }
                else
                {
                    sd.DAM = dam;
                    sd.InUse = !deleted;
                    ushort actualCrc = Lib.Crc(sd.DoubleDensity ? Floppy.CRC_RESET_A1_A1_A1 : Floppy.CRC_RESET, sd.DAM);
                    for (int i = 0; i < dataLength; i++)
                    {
                        sd.SectorData[i] = Data[dataStart + i * byteMultiple];
                        actualCrc = Lib.Crc(actualCrc, sd.SectorData[i]);
                    }
                    ushort recordedCrc = Lib.CombineBytes(Data[dataStart + (dataLength + 1) * byteMultiple], Data[dataStart + dataLength * byteMultiple]);
                    sd.CrcError = actualCrc != recordedCrc;
                }
                sds.Add(sd);
            }
            return sds.OrderBy(s => s.SectorNumber).ToList();
        }

        public IEnumerable<SectorDescriptor> ToSectorDescriptors() => GetSectorDescriptorCache();
        public bool DoubleDensity => density != false;
        public byte NumSectors => (byte)Header.Count(h => h > 0);
        public bool Formatted => NumSectors > 0;
        
        private byte FetchByte(ref int TrackIndex, ref ushort Crc, bool AllowResetCRC, bool DoubleDensityMode)
        {
            if (GetDensity(TrackIndex) != DoubleDensityMode)
            {
#if DEBUG
                throw new NotImplementedException("TODO: Handle case where reading from wrong density.");
#endif
            }
            byte b = 0;
            if (TrackIndex < Data.Length)
            {
                b = Data[TrackIndex];
                Crc = FloppyController.UpdateCRC(Crc, b, AllowResetCRC, DoubleDensityMode);
            }
            TrackIndex++;
            if (!DoubleDensityMode)
                TrackIndex++;
            return b;
        }
        public void RebuildHeader()
        {
            var header = new ushort[HEADER_LENGTH];

            int headerCursor = 0;

            int end = Data.Length - 10;
            int i = 4;
            while (i < end)
            {
                bool density = GetDensity(i);

                if (Data[i] == Floppy.IDAM)
                {
                    // is it a real IDAM? Check preceding bytes
                    if (density)
                    {
                        if (Data[i - 1] != 0xA1 || Data[i - 2] != 0xA1 || Data[i - 3] != 0xA1)
                        {
                            i++;
                            continue;
                        }
                    }
                    else
                    {
                        if (Data[i - 2] != 0x00)
                        {
                            i += 2;
                            continue;
                        }
                    }

                    // commit without checking address crc, since could be intentional error
                    header[headerCursor++] = (ushort)(i + HEADER_LENGTH_BYTES + (density ? DOUBLE_DENSITY_MASK : 0));

                    // now skip forward past the sector contents
                    // advance to length
                    if (density) i += 4; else i += 8;
                    i += Floppy.GetDataLengthFromCode(Data[i]) * (density ? 1 : 2);
                    i += 20 * (density ? 1 : 2); // filler minimum
                }
                else
                {
                    if (density)
                        i++;
                    else
                        i += 2;
                }
            }
            this.header = header;
        }
        public void RebuildDensity()
        {
            SetDensity();
            
            if (density.HasValue)
            {
                densityMap = null;
            }
            else
            {
                densityMap = new bool[Data.Length];

                int idamOffset = 0x10;
                bool d = Header[0] >= DOUBLE_DENSITY_MASK;
                int i = 0;
                for (int j = 1; j < Header.Length && Header[j] > 0; j++)
                {
                    int end = (Header[j] & OFFSET_MASK) - HEADER_LENGTH_BYTES - idamOffset;
                    for (; i < end; i++)
                        densityMap[i] = d;
                    d = Header[j] >= DOUBLE_DENSITY_MASK;
                }
                for (; i < densityMap.Length; i++)
                    densityMap[i] = d;
            }
        }
        private void ConvertFromSingleByte()
        {
            switch (density)
            {
                case true:
                    // do nothing: single byte is what we want 
                    break;
                case false:
                    // just duplicate bytes everywhere
                    Data = Data.Double().Truncate(MAX_LENGTH_WITH_HEADER);
                    for (int i = 0; i < Header.Length; i++)
                         Header[i] *= 2; // don't need to worry about the DD flag
                    break;
                default: // mixed density
                    RebuildDensity();

                    if (densityMap == null || densityMap.Length != Data.Length)
                        throw new Exception("Error generating density map in Track.ConvertFromSingleByte");

                    // Adjust the data
                    var sdDoubled = new List<byte>();
                    for (int i = 0; i < densityMap.Length; i++)
                    {
                        sdDoubled.Add(Data[i]);
                        if (!densityMap[i])
                            sdDoubled.Add(Data[i]);
                    }

                    Data = sdDoubled.ToArray();
                    Data = Data.Pad(DEFAULT_LENGTH_WITHOUT_HEADER, Floppy.FILLER_BYTE_DD);

                    for (int i = 0; i < Header.Length && Header[i] > 0; i++)
                        Header[i] += (ushort)(densityMap.Take((Header[i] & OFFSET_MASK) - HEADER_LENGTH_BYTES).Count(d => !d));

                    densityMap = null; // no longer valid

                    break;
            }
        }
        public override string ToString()
        {
            return string.Format($"Track {PhysicalTrackNum} Side {(SideOne ? 1 : 0)}");
        }
    }
}
