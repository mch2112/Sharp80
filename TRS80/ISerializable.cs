using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80
{
    public interface ISerializable
    {
        void Serialize(System.IO.BinaryWriter Writer);
        bool Deserialize(System.IO.BinaryReader Reader, int SerializationVersion);
    }
}
