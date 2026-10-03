using System;
using System.Buffers;

namespace Serde.CmdLine;

internal sealed partial class Deserializer
{
    /// <summary>
    /// Shared base for the type deserializers. Values are always read straight from the
    /// <see cref="Deserializer"/>, so only reading indices and ending the type differ.
    /// </summary>
    private abstract class TypeDeserializerBase(Deserializer deserializer) : ITypeDeserializer
    {
        protected readonly Deserializer _deserializer = deserializer;

        public abstract int TryReadIndex(ISerdeInfo info);

        public abstract void End(ISerdeInfo info);

        (int, string?) ITypeDeserializer.TryReadIndexWithName(ISerdeInfo info) => (TryReadIndex(info), null);

        IDeserializer ITypeDeserializer.ReadFieldStart(ISerdeInfo info, int index) => _deserializer;

        void ITypeDeserializer.ReadFieldEnd(ISerdeInfo info, int index, IDeserializer deserializer) { }

        int ITypeDeserializer.ReadEnum(ISerdeInfo typeInfo, int index, ISerdeInfo fieldInfo)
            => throw new NotSupportedException();

        int? ITypeDeserializer.SizeOpt => null;

        T ITypeDeserializer.ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) => deserialize.Deserialize(_deserializer);

        bool ITypeDeserializer.ReadBool(ISerdeInfo info, int index) => _deserializer.ReadBool();

        byte ITypeDeserializer.ReadU8(ISerdeInfo info, int index) => _deserializer.ReadU8();

        char ITypeDeserializer.ReadChar(ISerdeInfo info, int index) => _deserializer.ReadChar();

        decimal ITypeDeserializer.ReadDecimal(ISerdeInfo info, int index) => _deserializer.ReadDecimal();

        double ITypeDeserializer.ReadF64(ISerdeInfo info, int index) => _deserializer.ReadF64();

        float ITypeDeserializer.ReadF32(ISerdeInfo info, int index) => _deserializer.ReadF32();

        short ITypeDeserializer.ReadI16(ISerdeInfo info, int index) => _deserializer.ReadI16();

        int ITypeDeserializer.ReadI32(ISerdeInfo info, int index) => _deserializer.ReadI32();

        long ITypeDeserializer.ReadI64(ISerdeInfo info, int index) => _deserializer.ReadI64();

        sbyte ITypeDeserializer.ReadI8(ISerdeInfo info, int index) => _deserializer.ReadI8();

        string ITypeDeserializer.ReadString(ISerdeInfo info, int index)
        {
            return _deserializer.ReadString();
        }

        ushort ITypeDeserializer.ReadU16(ISerdeInfo info, int index) => _deserializer.ReadU16();

        uint ITypeDeserializer.ReadU32(ISerdeInfo info, int index) => _deserializer.ReadU32();

        ulong ITypeDeserializer.ReadU64(ISerdeInfo info, int index) => _deserializer.ReadU64();

        void ITypeDeserializer.SkipValue(ISerdeInfo info, int index) => _deserializer._argIndex++;

        DateTime ITypeDeserializer.ReadDateTime(ISerdeInfo info, int index)
            => _deserializer.ReadDateTime();

        DateTimeOffset ITypeDeserializer.ReadDateTimeOffset(ISerdeInfo info, int index)
            => _deserializer.ReadDateTimeOffset();

        Int128 ITypeDeserializer.ReadI128(ISerdeInfo info, int index)
            => _deserializer.ReadI128();

        UInt128 ITypeDeserializer.ReadU128(ISerdeInfo info, int index)
            => _deserializer.ReadU128();

        void ITypeDeserializer.ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer)
            => _deserializer.ReadBytes(writer);
    }
}
