using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;

namespace Serde.CmdLine;

internal sealed partial class Deserializer : ITypeDeserializer
{
    int ITypeDeserializer.TryReadIndex(ISerdeInfo serdeInfo)
    {
        var (index, _) = ((ITypeDeserializer)this).TryReadIndexWithName(serdeInfo);
        return index;
    }

    (int, string? errorName) ITypeDeserializer.TryReadIndexWithName(ISerdeInfo serdeInfo)
    {
        if (_argIndex == args.Length)
        {
            return (ITypeDeserializer.EndOfType, null);
        }

        var arg = args[_argIndex];
        while (_handleHelp && arg is "-h" or "--help")
        {
            _argIndex++;
            _helpInfos.Add(serdeInfo);
            if (_argIndex == args.Length)
            {
                return (ITypeDeserializer.EndOfType, null);
            }
            arg = args[_argIndex];
        }

        for (int fieldIndex = 0; fieldIndex < serdeInfo.FieldCount; fieldIndex++)
        {
            IList<CustomAttributeData> attrs = serdeInfo.GetFieldAttributes(fieldIndex);
            foreach (var attr in attrs)
            {
                if (arg.StartsWith('-') &&
                    attr is { AttributeType: { Name: nameof(CommandOptionAttribute) },
                              ConstructorArguments: [ { Value: string flagNames } ] })
                {
                    var flagNamesArray = flagNames.Split('|');
                    foreach (var flag in flagNamesArray)
                    {
                        if (arg == flag)
                        {
                            _argIndex++;
                            return (fieldIndex, null);
                        }
                    }
                }
                else if (!arg.StartsWith('-') &&
                         attr is { AttributeType: { Name: nameof(CommandAttribute) },
                                   ConstructorArguments: [ { Value: string commandName } ] } &&
                         commandName == arg)
                {
                    _argIndex++;
                    return (fieldIndex, null);
                }
                else if (!arg.StartsWith('-') &&
                         attr is { AttributeType: { Name: nameof(CommandGroupAttribute) } })
                {
                    // If the field is a command group, check to see if any of the nested commands match
                    // the argument. If so, mark this field as a match.
#pragma warning disable SerdeExperimentalFieldInfo // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    var fieldInfo = serdeInfo.GetFieldInfo(fieldIndex);
                    if (fieldInfo.Kind == InfoKind.Nullable)
                    {
                        // Unwrap nullable if present
                        fieldInfo = fieldInfo.GetFieldInfo(0);
                    }
#pragma warning restore SerdeExperimentalFieldInfo // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                    // Save the argIndex and throwOnMissing so we can restore it after checking.
                    var savedIndex = _argIndex;
                    var savedThrowOnMissing = _throwOnMissing;
                    _throwOnMissing = false;

                    var deType = this.ReadType(fieldInfo);
                    int index = deType.TryReadIndex(fieldInfo);
                    _argIndex = savedIndex;
                    _throwOnMissing = savedThrowOnMissing;

                    if (index >= 0)
                    {
                        // We found a match, so we can return the field index.
                        return (fieldIndex, null);
                    }
                    // No match, so we can continue.
                }
                else if (!arg.StartsWith('-') &&
                         attr is { AttributeType: { Name: nameof(CommandParameterAttribute) },
                                   ConstructorArguments: [ { Value: int paramIndex }, _ ] } &&
                         _paramIndex == paramIndex)
                {
                    _paramIndex++;
                    return (fieldIndex, null);
                }
            }
        }
        if (_throwOnMissing)
        {
            throw new ArgumentSyntaxException($"Unexpected argument: '{arg}'");
        }
        else
        {
            return (ITypeDeserializer.IndexNotFound, arg);
        }
    }
    int? ITypeDeserializer.SizeOpt => null;

    T ITypeDeserializer.ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) => deserialize.Deserialize(this);

    bool ITypeDeserializer.ReadBool(ISerdeInfo info, int index) => ReadBool();

    byte ITypeDeserializer.ReadU8(ISerdeInfo info, int index) => ReadU8();

    char ITypeDeserializer.ReadChar(ISerdeInfo info, int index) => ReadChar();

    decimal ITypeDeserializer.ReadDecimal(ISerdeInfo info, int index) => ReadDecimal();

    double ITypeDeserializer.ReadF64(ISerdeInfo info, int index) => ReadF64();

    float ITypeDeserializer.ReadF32(ISerdeInfo info, int index) => ReadF32();

    short ITypeDeserializer.ReadI16(ISerdeInfo info, int index) => ReadI16();

    int ITypeDeserializer.ReadI32(ISerdeInfo info, int index) => ReadI32();

    long ITypeDeserializer.ReadI64(ISerdeInfo info, int index) => ReadI64();

    sbyte ITypeDeserializer.ReadI8(ISerdeInfo info, int index) => ReadI8();

    string ITypeDeserializer.ReadString(ISerdeInfo info, int index) => ReadString();

    ushort ITypeDeserializer.ReadU16(ISerdeInfo info, int index) => ReadU16();

    uint ITypeDeserializer.ReadU32(ISerdeInfo info, int index) => ReadU32();

    ulong ITypeDeserializer.ReadU64(ISerdeInfo info, int index) => ReadU64();

    void ITypeDeserializer.SkipValue(ISerdeInfo info, int index) => _argIndex++;

    DateTime ITypeDeserializer.ReadDateTime(ISerdeInfo info, int index)
        => ReadDateTime();

    void ITypeDeserializer.ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer)
        => ReadBytes(writer);
}