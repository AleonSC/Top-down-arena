using System.IO;

namespace Arena
{
    /// One pass over a message's fields that either writes them (the host) or reads them back (a client), so
    /// both sides go through the same list in the same order and can't drift apart. Each call takes the field by
    /// ref: writing sends its value, reading overwrites it. A field that needs care when read (a player's own
    /// position, a flash when health drops) is synced into a local and applied by the caller.
    public sealed class NetSync
    {
        readonly BinaryWriter w;
        readonly BinaryReader r;

        public NetSync(BinaryWriter writer) { w = writer; }
        public NetSync(BinaryReader reader) { r = reader; }

        public bool Reading => r != null;

        public void Float(ref float v) { if (r != null) v = r.ReadSingle(); else w.Write(v); }
        public void Int(ref int v) { if (r != null) v = r.ReadInt32(); else w.Write(v); }
        public void Bool(ref bool v) { if (r != null) v = r.ReadBoolean(); else w.Write(v); }
        public void Vec(ref V2 v) { if (r != null) v = r.ReadV2(); else w.Write(v); }
        public void Byte(ref byte v) { if (r != null) v = r.ReadByte(); else w.Write(v); }
        public void SByte(ref int v) { if (r != null) v = r.ReadSByte(); else w.Write((sbyte)v); }

        /// An int sent as one byte (0-255).
        public void Byte(ref int v) { if (r != null) v = r.ReadByte(); else w.Write((byte)v); }

        /// An int sent as two bytes.
        public void Short(ref int v) { if (r != null) v = r.ReadInt16(); else w.Write((short)v); }
    }
}
