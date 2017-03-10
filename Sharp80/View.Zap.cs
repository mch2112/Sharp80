/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal class ViewZap : View
    {
        protected override ViewMode Mode => ViewMode.Zap;
        protected override bool ForceRedraw => false;

        private bool sideOne = false;
        private byte trackNum = 0;
        private byte sectorIndex = 0;

        protected override void Activate()
        {
            base.Activate();
        }

        protected override bool processKey(KeyState Key)
        {
            if (!Key.IsUnmodified || !Key.Pressed)
                return base.processKey(Key);

            bool processed = false;

            switch (Key.Key)
            {
                case KeyCode.Tab:
                    DriveNumber = DriveNumber ?? 0;
                    for (int i = DriveNumber.Value + 1; i < DriveNumber.Value + 5; i++)
                    {
                        DriveNumber = (byte)(i % FloppyController.NUM_DRIVES);
                        if (!Computer.DriveIsUnloaded(DriveNumber.Value))
                            break;
                    }
                    VerifyZapParamsOK();
                    processed = true;
                    break;
                case KeyCode.Space:
                    sideOne = !sideOne;
                    VerifyZapParamsOK();
                    processed = true;
                    break;
                case KeyCode.Left:
                    if (sectorIndex > 0)
                    {
                        sectorIndex--;
                        VerifyZapParamsOK();
                        processed = true;
                    }
                    break;
                case KeyCode.Right:
                    if (sectorIndex < 0xFD)
                    {
                        sectorIndex++;
                        VerifyZapParamsOK();
                        processed = true;
                    }
                    break;
                case KeyCode.PageUp:
                case KeyCode.Up:
                    if (Key.Shift)
                        if (trackNum > 10)
                            trackNum -= 10;
                        else
                            trackNum = 0;
                    else
                        trackNum--;
                    VerifyZapParamsOK();
                    processed = true;
                    break;
                case KeyCode.PageDown:
                case KeyCode.Down:
                    if (Key.Shift)
                        trackNum += 10;
                    else
                        trackNum++;
                    VerifyZapParamsOK();
                    processed = true;
                    break;
            }

            if (processed)
                Invalidate();

            return processed || base.processKey(Key);
        }
        protected override byte[] GetViewBytes()
        {
            DriveNumber = DriveNumber ?? 0;

            var f = Computer.GetFloppy(DriveNumber.Value);
            var sd = f.GetSectorDescriptor(trackNum, sideOne, sectorIndex);

            int numBytes = Math.Min(0x100, sd?.SectorData?.Length ?? 0);

            byte[] cells = new byte[ScreenMetrics.NUM_SCREEN_CHARS];

            WriteToByteArray(cells, 0x000, "Dsk");
            cells[0x040] = DriveNumber.Value.ToHexCharByte();

            WriteToByteArray(cells, 0x0C0, "Trk");
            WriteToByteArrayHex(cells, 0x100, trackNum);

            if (f.DoubleSided)
            {
                WriteToByteArray(cells, 0x280, "Side");
                cells[0x300] = (byte)(sideOne ? '1' : '0');
            }

            if (sd != null)
            {
                WriteToByteArray(cells, 0x180, "Sec");
                WriteToByteArrayHex(cells, 0x1C0, sd.SectorNumber);

                WriteToByteArray(cells, 0x200, sd.DoubleDensity ? "DD" : "SD");

                if (sd.TrackNumber != trackNum)
                    WriteToByteArrayHex(cells, 0x140, sd.TrackNumber);

                switch (sd.DAM)
                {
                    case Floppy.DAM_NORMAL:
                        WriteToByteArray(cells, 0x300, "Std");
                        break;
                    case Floppy.DAM_DELETED:
                        WriteToByteArray(cells, 0x300, "Del");
                        break;
                }
                if (sd.CrcError)
                    WriteToByteArray(cells, 0x380, "CRC");
            }

            if (f == null)
            {
                WriteToByteArray(cells, 0x006, string.Format("Drive {0} is empty.", DriveNumber));
            }
            else if (sd == null || numBytes == 0)
            {
                WriteToByteArray(cells, 0x006, "Sector is empty.");
            }
            else
            {
                int cell = 0;
                int rawCell = 0x30;

                for (int k = 0; k < 0x100; k++)
                {
                    if ((k & 0x0F) == 0x00)
                    {
                        // new line
                        cell += 0x05;
                        WriteToByteArrayHex(cells, cell, (byte)k);
                        cell += 2;
                    }
                    if (k < numBytes)
                    {
                        if (k % 2 == 0)
                            cell++;

                        byte b = sd.SectorData[k];

                        WriteToByteArrayHex(cells, cell, b);
                        cell += 2;

                        cells[rawCell++] = b;

                        if ((k & 0x0F) == 0x0F)
                        {
                            // wrap to new line on screen
                            rawCell += 0x30;
                            cell += 0x20 - 15;
                        }
                    }
                    else if ((k & 0x0F) == 0x00)
                    {
                        cell = k / 0x10 * ScreenMetrics.NUM_SCREEN_CHARS_X;
                    }
                }
            }
            return cells;
        }
        private void VerifyZapParamsOK()
        {
            DriveNumber = DriveNumber ?? 0;

            IFloppy f = null;

            for (byte i = DriveNumber.Value; i < DriveNumber.Value + FloppyController.NUM_DRIVES; i++)
            {
                f = Computer.GetFloppy((byte)(i % FloppyController.NUM_DRIVES));
                if (f != null)
                {
                    DriveNumber = i;
                    break;
                }
            }
            if (f == null)
            {
                sideOne = false;
                trackNum = 0;
                sectorIndex = 0;
            }
            else
            {
                if (!f.DoubleSided)
                    sideOne = false;

                if (trackNum == 0xFF)
                    trackNum = 0;

                if (trackNum >= f.NumTracks)
                    trackNum = (byte)(Math.Max(0, f.NumTracks - 1));

                if (sectorIndex >= f.SectorCount(trackNum, sideOne))
                    sectorIndex = (byte)(f.SectorCount(trackNum, sideOne) - 1);
                else if (sectorIndex < 0)
                    sectorIndex = 0;
            }
        }
    }
}
