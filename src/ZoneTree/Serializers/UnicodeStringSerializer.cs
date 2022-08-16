﻿using System.Text;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

public class UnicodeStringSerializer : ISerializer<string>
{
    public string Deserialize(byte[] bytes)
    {
        if (bytes.Length == 1)
            return null;
        return Encoding.Unicode.GetString(bytes);
    }

    public byte[] Serialize(in string entry)
    {
        if (entry == null)
            return new byte[1] { 0 };
        return Encoding.Unicode.GetBytes(entry);
    }
}
