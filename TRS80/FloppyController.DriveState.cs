using System;
using System.IO;

namespace Sharp80.TRS80
{
    public partial class FloppyController : ISerializable
    {
        private class DriveState
        {
            public Floppy Floppy { get; set; }
            public byte PhysicalTrackNumber { get; set; }
            public bool IsLoaded => Floppy != null;
            public bool IsUnloaded => Floppy == null;
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
                    if (Reader.ReadBoolean())
                        Floppy = new DMK(Reader);
                    else
                        Floppy = null;
                    PhysicalTrackNumber = Reader.ReadByte();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
