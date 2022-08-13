﻿using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

public class ByteSerializer : ISerializer<byte>
{
    public byte Deserialize(byte[] bytes)
    {
        return bytes[0];
    }

    public byte[] Serialize(in byte entry)
    {
        return new byte[1] { entry };
    }
}
