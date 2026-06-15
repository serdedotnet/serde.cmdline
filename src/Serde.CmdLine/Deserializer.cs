using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

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

    public char ReadChar() => ReadString()[0];

    public byte ReadU8() => byte.Parse(ReadString(), CultureInfo.InvariantCulture);

    public ushort ReadU16() => ushort.Parse(ReadString(), CultureInfo.InvariantCulture);

    public uint ReadU32() => uint.Parse(ReadString(), CultureInfo.InvariantCulture);

    public ulong ReadU64() => ulong.Parse(ReadString(), CultureInfo.InvariantCulture);

    public sbyte ReadI8() => sbyte.Parse(ReadString(), CultureInfo.InvariantCulture);

    public short ReadI16() => short.Parse(ReadString(), CultureInfo.InvariantCulture);

    public int ReadI32() => int.Parse(ReadString(), CultureInfo.InvariantCulture);

    public long ReadI64() => long.Parse(ReadString(), CultureInfo.InvariantCulture);

    public float ReadF32() => float.Parse(ReadString(), CultureInfo.InvariantCulture);

    public double ReadF64() => double.Parse(ReadString(), CultureInfo.InvariantCulture);

    public decimal ReadDecimal() => decimal.Parse(ReadString(), CultureInfo.InvariantCulture);
    public DateTime ReadDateTime() => throw new NotImplementedException();
    public DateTimeOffset ReadDateTimeOffset() => throw new NotImplementedException();
    public Int128 ReadI128() => Int128.Parse(ReadString(), CultureInfo.InvariantCulture);
    public UInt128 ReadU128() => UInt128.Parse(ReadString(), CultureInfo.InvariantCulture);
    public void ReadBytes(IBufferWriter<byte> writer) => throw new NotImplementedException();

    public void Dispose()
    {
        if (_skippedOptions.Count > 0)
        {
            throw new ArgumentSyntaxException($"Unexpected argument: '{_skippedOptions[0]}'");
        }
    }
}