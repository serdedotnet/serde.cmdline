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
    // Set once "--" is seen. After that, every arg is a parameter, as in getopt.
    private bool _endOfOptions = false;

    public IReadOnlyList<ISerdeInfo> HelpInfos => _helpInfos;

    public ITypeDeserializer ReadType(ISerdeInfo typeInfo)
    {
        if (typeInfo.Kind == InfoKind.List)
        {
            // Only variadic parameters are collections, so the list belongs to the current command.
            return new DeserializeCollection(this, _commandStack[^1]);
        }
        var cmd = DeserializeType.ParseCommand(typeInfo);
        _commandStack.Add(cmd);
        return new DeserializeType(this, cmd);
    }

    /// <summary>
    /// Consume the current arg if it is "--" or a help flag, which apply wherever they appear.
    /// </summary>
    private bool TryConsumeSpecialArg(ISerdeInfo commandInfo)
    {
        if (_endOfOptions)
        {
            return false;
        }
        var arg = _args[_argIndex];
        if (arg == "--")
        {
            _endOfOptions = true;
            _argIndex++;
            return true;
        }
        if (_handleHelp && arg is "-h" or "--help")
        {
            _helpInfos.Add(commandInfo);
            _argIndex++;
            return true;
        }
        return false;
    }

    private bool IsOption(string arg) => !_endOfOptions && arg.StartsWith('-');

    /// <summary>
    /// Check whether the current arg is an option of the command at <paramref name="depth"/> in
    /// the command stack, or of any of its parents. If so, record the option and its value in
    /// <paramref name="skipped"/> and advance past them. The value can't be parsed yet because the
    /// field belongs to a type that isn't being read right now.
    /// </summary>
    private bool TrySkipOption(int depth, List<string> skipped)
    {
        var arg = _args[_argIndex];
        for (int ci = depth; ci >= 0; ci--)
        {
            foreach (var option in _commandStack[ci].Options)
            {
                if (option.FlagNames.Contains(arg))
                {
                    if (option.HasArg && _argIndex + 1 == _args.Length)
                    {
                        throw new ArgumentSyntaxException($"Option '{arg}' requires a value.");
                    }
                    skipped.Add(arg);
                    _argIndex++;
                    // If this is not a bool flag, we need to skip the next arg as well
                    if (option.HasArg)
                    {
                        skipped.Add(_args[_argIndex]);
                        _argIndex++;
                    }
                    return true;
                }
            }
        }
        return false;
    }

    public bool ReadBool()
    {
        // Assume that if we got here we saw a flag option
        return true;
    }

    public bool TryReadNull()
    {
        // A field's value is only read when its corresponding argument is present, so there is
        // always a value to read.
        return false;
    }

    public int ReadEnum(ISerdeInfo info) => throw new NotSupportedException();

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

    /// <summary>
    /// Check that every skipped option was read by the command that owns it. Call this only after
    /// deserialization succeeds, so it can't hide an earlier error.
    /// </summary>
    public void CheckAllArgsConsumed()
    {
        if (_skippedOptions.Count > 0)
        {
            throw new ArgumentSyntaxException($"Unexpected argument: '{_skippedOptions[0]}'");
        }
    }

    public void Dispose() { }
}