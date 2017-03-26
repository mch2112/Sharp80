using System;
using System.IO;

namespace Sharp80
{
    internal partial class FloppyController : ISerializable
    {
        private class DriveState
        {
            public Floppy Floppy { get; set; }
            public bool IsLoaded { get { return Floppy != null; } }
            public bool IsUnloaded { get { return Floppy == null; } }
            public bool OnTrackZero { get { return PhysicalTrackNumber == 0; } }
            public byte PhysicalTrackNumber { get; set; }
            public bool WriteProtected { get { return Floppy?.WriteProtected ?? true; } set { Floppy.WriteProtected = value; } }
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
