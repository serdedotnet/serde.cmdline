using System;
using System.Buffers;
using System.Collections.Generic;

namespace Serde.CmdLine;

internal sealed partial class Deserializer(string[] args, bool handleHelp) : IDeserializer
{
    private readonly string[] _args = args;
    private readonly bool _handleHelp = handleHelp;
    private int _argIndex = 0;
    private int _paramIndex = 0;
    private readonly List<ISerdeInfo> _helpInfos = new();
    private readonly Stack<Command> _parentCommands = new();
    // Queue of (fieldIndex, argIndex) pairs for parent options found after subcommand
    private readonly Queue<(int FieldIndex, int ArgIndex)> _pendingParentOptions = new();
    // Set of argument indices that have been claimed by parent commands (to skip in subcommand parsing)
    private readonly HashSet<int> _claimedArgIndices = new();

    public IReadOnlyList<ISerdeInfo> HelpInfos => _helpInfos;

    public ITypeDeserializer ReadType(ISerdeInfo typeInfo) => new DeserializeType(this, typeInfo);

    public bool ReadBool()
    {
        // Flags are a little tricky. They can be specified as --flag or '--flag true' or '--flag false'.
        // There's no way to know for sure whether the current argument is a flag or a value, so we'll
        // try to parse it as a bool. If it fails, we'll assume it's a flag and return true.
        if (_argIndex == _args.Length || !bool.TryParse(_args[_argIndex], out bool value))
        {
            return true;
        }
        _argIndex++;
        return value;
    }

    public string ReadString() => _args[_argIndex++];

    public T ReadNullableRef<T>(IDeserialize<T> d)
        where T : class
    {
        // Treat all nullable values as just being optional. Since we got here we must have a value
        // in hand.
        return d.Deserialize(this);
    }

    public char ReadChar() => throw new NotImplementedException();

    public byte ReadU8() => throw new NotImplementedException();

    public ushort ReadU16() => throw new NotImplementedException();

    public uint ReadU32() => throw new NotImplementedException();

    public ulong ReadU64() => throw new NotImplementedException();

    public sbyte ReadI8() => throw new NotImplementedException();

    public short ReadI16() => throw new NotImplementedException();

    public int ReadI32() => throw new NotImplementedException();

    public long ReadI64() => throw new NotImplementedException();

    public float ReadF32() => throw new NotImplementedException();

    public double ReadF64() => throw new NotImplementedException();

    public decimal ReadDecimal() => throw new NotImplementedException();
    public DateTime ReadDateTime() => throw new NotImplementedException();
    public void ReadBytes(IBufferWriter<byte> writer) => throw new NotImplementedException();

    public void Dispose() { }
}