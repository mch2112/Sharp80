/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.Z80
{
    public interface ISerializable
    {
        void Serialize(System.IO.BinaryWriter Writer);
        bool Deserialize(System.IO.BinaryReader Reader, int SerializationVersion);
    }
}
