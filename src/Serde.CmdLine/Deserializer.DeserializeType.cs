using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace Serde.CmdLine;

internal sealed partial class Deserializer
{
    private sealed class DeserializeType(
        Deserializer _deserializer,
        Command _command
    ) : ITypeDeserializer
    {
        private readonly List<string> _skippedOptions = new();

        void IDisposable.Dispose()
        {
            // Pop the command stack
            _deserializer._commandStack.RemoveAt(_deserializer._commandStack.Count - 1);
            _deserializer._checkingSkipped = false;
        }

        (int, string?) ITypeDeserializer.TryReadIndexWithName(ISerdeInfo serdeInfo) => (TryReadIndex(serdeInfo), null);

        private int TryReadIndex(ISerdeInfo serdeInfo)
        {
            if (_deserializer._checkingSkipped)
            {
                return CheckSkippedOptions();
            }

            // Loop until we find a matching field, or run out of args
            ref int argIndex = ref _deserializer._argIndex;
            string[] args = _deserializer._args;
            while (true)
            {
                if (argIndex > args.Length)
                {
                    throw new InvalidOperationException("Argument index exceeded argument length.");
                }

                if (argIndex == args.Length)
                {
                    _deserializer._checkingSkipped = true;
                    return CheckSkippedOptions();
                }

                var arg = args[argIndex];
                if (_deserializer._handleHelp && arg is "-h" or "--help")
                {
                    argIndex++;
                    _deserializer._helpInfos.Add(serdeInfo);
                    continue;
                }

                var (fieldIndex, incArgs) = CheckFields(arg);
                if (fieldIndex >= 0)
                {
                    if (incArgs)
                    {
                        argIndex++;
                    }
                    return fieldIndex;
                }

                // No match, so check parent options
                if (arg.StartsWith('-') && IsParentOption(args, ref argIndex))
                {
                    continue;
                }

                // Unrecognized argument
                throw new ArgumentSyntaxException($"Unexpected argument: '{arg}'");
            }
        }

        /// <summary>
        /// Given a list of args and an arg index, check to see if the current arg matches any
        /// options from parent commands.  If a match is found, advance the arg index appropriately
        /// and record the skipped option.
        /// </summary>
        private bool IsParentOption(ReadOnlySpan<string> args, ref int argIndex)
        {
            var arg = args[argIndex];
            // It's an option we don't recognize, so we will check the parent deserializer.
            // We need to immediately check if it's valid because we need to know how many
            // args to skip. However, the actual value parsing needs to be done later because
            // the parent field is part of the parent type.
            // N.B. The top of the stack is the current command
            for (int ci = _deserializer._commandStack.Count - 2; ci >= 0; ci--)
            {
                var parentCmd = _deserializer._commandStack[ci];
                foreach (var option in parentCmd.Options)
                {
                    foreach (var name in option.FlagNames)
                    {
                        if (name == arg)
                        {
                            _skippedOptions.Add(arg);
                            argIndex++;
                            // If this is not a bool flag, we need to skip the next arg as well
                            if (option.HasArg)
                            {
                                _skippedOptions.Add(args[argIndex]);
                                argIndex++;
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private int CheckSkippedOptions()
        {
            // Before we leave we need to check all skipped options, then add any skipped options
            // we've recorded to the parent deserializer.
            for (int skipIndex = 0; skipIndex < _deserializer._skippedOptions.Count; skipIndex++)
            {
                var skipped = _deserializer._skippedOptions[skipIndex];
                if (CheckOptions(_command, skipped) is {} opt)
                {
                    _deserializer._skippedOptions.RemoveAt(skipIndex);
                    _deserializer._skipIndex = skipIndex;
                    return opt.FieldIndex;
                }
            }
            _deserializer._skippedOptions.AddRange(_skippedOptions);
            _skippedOptions.Clear();
            return ITypeDeserializer.EndOfType;
        }

        int ITypeDeserializer.TryReadIndex(ISerdeInfo info)
        {
            return TryReadIndex(info);
        }

        /// <summary>
        /// Check if the given argument matches any options in the current command.
        /// </summary>
        private static Option? CheckOptions(Command cmd, string arg)
        {
            foreach (var option in cmd.Options)
            {
                foreach (var name in option.FlagNames)
                {
                    if (name == arg)
                    {
                        return option;
                    }
                }
            }
            return null;
        }

        private (int fieldIndex, bool incArgs) CheckFields(string arg)
        {
            var cmd = _command;
            if (arg.StartsWith('-'))
            {
                var fieldIndex = CheckOptions(cmd, arg)?.FieldIndex ?? -1;
                return (fieldIndex, fieldIndex >= 0);
            }

            // Check for command group matches
            foreach (var subCmd in cmd.SubCommands)
            {
                if (arg == subCmd.Name)
                {
                    return (subCmd.FieldIndex, true);
                }
            }

            foreach (var cmdGroup in cmd.CommandGroups)
            {
                foreach (var name in cmdGroup.CommandNames)
                {
                    if (name == arg)
                    {
                        return (cmdGroup.FieldIndex, false);
                    }
                }

                // No match, so we can continue.
            }

            // Check for parameter matches
            foreach (var param in cmd.Parameters)
            {
                // Parameters are positional, so we check the current param index
                if (_deserializer._paramIndex == param.Ordinal)
                {
                    return (param.FieldIndex, false);
                }
            }
            return (-1, false);
        }

        public static Command ParseCommand(ISerdeInfo serdeInfo)
        {
            var options = ImmutableArray.CreateBuilder<Option>();
            var subCmdNames = ImmutableArray.CreateBuilder<SubCommand>();
            var cmdGroups = ImmutableArray.CreateBuilder<CommandGroup>();
            var parameters = ImmutableArray.CreateBuilder<Parameter>();
            for (int fieldIndex = 0; fieldIndex < serdeInfo.FieldCount; fieldIndex++)
            {
                IList<CustomAttributeData> attrs = serdeInfo.GetFieldAttributes(fieldIndex);
                foreach (var attr in attrs)
                {
                    if (attr is
                        {
                            AttributeType: { Name: nameof(CommandOptionAttribute) },
                            ConstructorArguments: [{ Value: string flagNames }]
                        })
                    {
                        var flagNamesArray = flagNames.Split('|');
#pragma warning disable SerdeExperimentalFieldInfo // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        var fieldInfo = serdeInfo.GetFieldInfo(fieldIndex);
                        if (fieldInfo.Kind == InfoKind.Nullable)
                        {
                            // Unwrap nullable if present
                            fieldInfo = fieldInfo.GetFieldInfo(0);
                        }
                        var hasArg = fieldInfo.Name == "bool" ? false : true;
#pragma warning restore SerdeExperimentalFieldInfo // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        options.Add(new Option(flagNamesArray.ToImmutableArray(), fieldIndex, hasArg));
                    }
                    else if (attr is
                        {
                            AttributeType: { Name: nameof(CommandAttribute) },
                            ConstructorArguments: [{ Value: string commandName }]
                        })
                    {
                        subCmdNames.Add(new(commandName, fieldIndex));
                    }
                    else if (attr is { AttributeType: { Name: nameof(CommandGroupAttribute) } })
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

                        var groupInfo = (IUnionSerdeInfo)fieldInfo;
                        var cmdNames = ImmutableArray.CreateBuilder<string>();
                        foreach (var caseInfo in groupInfo.CaseInfos)
                        {
                            string? foundName = null;
                            foreach (var caseAttr in caseInfo.Attributes)
                            {
                                if (caseAttr is
                                    {
                                        AttributeType: { Name: nameof(CommandAttribute) },
                                        ConstructorArguments: [{ Value: string cmdName }]
                                    })
                                {
                                    foundName = cmdName;
                                    break;
                                }
                            }
                            if (foundName is null)
                            {
                                throw new InvalidOperationException(
                                    $"CommandGroup case '{caseInfo.Name}' is missing CommandAttribute."
                                );
                            }
                            cmdNames.Add(foundName);
                        }

                        cmdGroups.Add(new CommandGroup(fieldIndex, fieldInfo, cmdNames.ToImmutable()));

                        // No match, so we can continue.
                    }
                    else if (attr is
                        {
                            AttributeType: { Name: nameof(CommandParameterAttribute) },
                            ConstructorArguments: [{ Value: int paramIndex }, _]
                        })
                    {
                        parameters.Add(new(paramIndex, fieldIndex));
                    }
                }
            }
            return new(
                options.ToImmutable(),
                subCmdNames.ToImmutable(),
                cmdGroups.ToImmutable(),
                parameters.ToImmutable()
            );
        }


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