using System;

namespace LostPolygon.Common.SimpleXmlSerialization {
    [Flags]
    public enum SimpleXmlSerializerFlags {
        IsOptional = 1 << 0,
        CollectionUnorderedRequired = 1 << 3,
        CollectionOrdered = 1 << 4,
    }
}
