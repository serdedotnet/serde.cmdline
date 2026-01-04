using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Serde.CmdLine;

internal sealed partial class Deserializer(string[] args, bool handleHelp) : IDeserializer
{
    private readonly string[] _args = args;
    private readonly bool _handleHelp = handleHelp;
    private int _argIndex = 0;
    private int _paramIndex = 0;
    private readonly List<ISerdeInfo> _helpInfos = new();
    // We keep a stack of commands so nested commands can check parent options.
    private readonly List<Command> _commandStack = new();
    // We keep a list of skipped options because options from parent commands are inherited by
    // subcommands.
    private readonly List<string> _skippedOptions = new();
    private bool _checkingSkipped = false;
    private int _skipIndex = -1;

    public IReadOnlyList<ISerdeInfo> HelpInfos => _helpInfos;

    public ITypeDeserializer ReadType(ISerdeInfo typeInfo)
    {
        var cmd = DeserializeType.ParseCommand(typeInfo);
        _commandStack.Add(cmd);
        return new DeserializeType(this, cmd);
    }

    public bool ReadBool()
    {
        // Assume that if we got here we saw a flag option
        return true;
    }

    public string ReadString()
    {
        if (_checkingSkipped)
        {
            var str = _skippedOptions[_skipIndex];
            _skippedOptions.RemoveAt(_skipIndex);
            _skipIndex = -1;
            return str;
        }
        else
        {
            return _args[_argIndex++];
        }
    }

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

    public void Dispose()
    {
        if (_skippedOptions.Count > 0)
        {
            throw new ArgumentSyntaxException($"Unexpected argument: '{_skippedOptions[0]}'");
        }
    }
}