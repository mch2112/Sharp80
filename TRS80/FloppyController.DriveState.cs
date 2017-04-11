/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;

namespace Sharp80.TRS80
{
    public partial class FloppyController : ISerializable
    {
        private class DriveState
        {
            public IFloppy Floppy { get; set; }
            public byte PhysicalTrackNumber { get; set; }
            public bool IsLoaded => Floppy != null;
            public bool IsUnloaded => Floppy is null;
            public bool OnTrackZero => PhysicalTrackNumber == 0;

            public bool WriteProtected
            {
                get => Floppy?.WriteProtected ?? true;
                set => Floppy.WriteProtected = value;
            }
            public DriveState()
            {
                PhysicalTrackNumber = 0;
                Floppy = null;
            }
            public void Serialize(BinaryWriter Writer)
            {
                Writer.Write(Floppy != null);
                if (Floppy != null)
                    Floppy.Serialize(Writer);
                Writer.Write(PhysicalTrackNumber);
            }
            public bool Deserialize(BinaryReader Reader, int SerializationVersion)
            {
                try
                {
                    var f = new Floppy();
                    if (Reader.ReadBoolean())
                        f.Deserialize(Reader, SerializationVersion);
                    PhysicalTrackNumber = Reader.ReadByte();
                    if (f.Valid)
                        Floppy = f;
                    return true;
                }
                catch
                {
                    Floppy = null;
                    return false;
                }
            }
        }
    }
}
