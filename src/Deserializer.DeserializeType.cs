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
        ISerdeInfo serdeInfo
    ) : ITypeDeserializer
    {
        private readonly Command _command = ParseCommand(serdeInfo);
        private readonly List<string> _skippedOptions = new();

        (int, string?) ITypeDeserializer.TryReadIndexWithName(ISerdeInfo serdeInfo) => TryReadIndexWithName(serdeInfo);

        private (int, string?) TryReadIndexWithName(ISerdeInfo serdeInfo)
        {
            if (_deserializer._argIndex == _deserializer._args.Length)
            {
                goto endOfType;
            }

            var arg = _deserializer._args[_deserializer._argIndex];
            while (_deserializer._handleHelp && arg is "-h" or "--help")
            {
                _deserializer._argIndex++;
                _deserializer._helpInfos.Add(serdeInfo);
                if (_deserializer._argIndex == _deserializer._args.Length)
                {
                    goto endOfType;
                }
                arg = _deserializer._args[_deserializer._argIndex];
            }

            var (fieldIndex, incArgs,errorName) = CheckFields(arg);
            if (fieldIndex >= 0)
            {
                if (incArgs)
                {
                    _deserializer._argIndex++;
                }
                return (fieldIndex, errorName);
            }

            if (arg.StartsWith('-'))
            {
                // It's an option we don't recognize, so skip it for checking later
                _skippedOptions.Add(arg);
                // Don't skip the arg, since the SkipValue call will do that
                return (ITypeDeserializer.IndexNotFound, null);
            }

            throw new ArgumentSyntaxException($"Unexpected argument: '{arg}'");

        endOfType:
            // Before we leave we need to check all skipped options, then add any skipped options
            // we've recorded to the parent deserializer.
            for (int i = 0; i < _deserializer._skippedOptions.Count; i++)
            {
                var skipped = _deserializer._skippedOptions[i];
                var (skippedIndex, _, _) = CheckFields(skipped);
                if (skippedIndex >= 0)
                {
                    _deserializer._skippedOptions.RemoveAt(i);
                    return (skippedIndex, null);
                }
            }
            _deserializer._skippedOptions.AddRange(_skippedOptions);
            _skippedOptions.Clear();
            return (ITypeDeserializer.EndOfType, null);
        }

        int ITypeDeserializer.TryReadIndex(ISerdeInfo info)
        {
            var (fieldIndex, _) = TryReadIndexWithName(info);
            return fieldIndex;
        }

        private (int fieldIndex, bool incArgs, string? errorName) CheckFields(string arg)
        {
            var cmd = _command;
            if (arg.StartsWith('-'))
            {
                // It's an option, so check options
                foreach (var option in cmd.Options)
                {
                    foreach (var name in option.FlagNames)
                    {
                        if (name == arg)
                        {
                            return (option.FieldIndex, true, null);
                        }
                    }
                }
                // No option match, return missing
                return (-1, false, null);
            }

            // Check for command group matches
            foreach (var subCmd in cmd.SubCommands)
            {
                if (arg == subCmd.Name)
                {
                    return (subCmd.FieldIndex, true, null);
                }
            }

            foreach (var cmdGroup in cmd.CommandGroups)
            {
                foreach (var name in cmdGroup.CommandNames)
                {
                    if (name == arg)
                    {
                        return (cmdGroup.FieldIndex, false, null);
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
                    return (param.FieldIndex, true, null);
                }
            }
            return (-1, false, null);
        }

        private static Command ParseCommand(ISerdeInfo serdeInfo)
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
                        options.Add(new Option(flagNamesArray.ToImmutableArray(), fieldIndex));
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

        string ITypeDeserializer.ReadString(ISerdeInfo info, int index) => _deserializer.ReadString();

        ushort ITypeDeserializer.ReadU16(ISerdeInfo info, int index) => _deserializer.ReadU16();

        uint ITypeDeserializer.ReadU32(ISerdeInfo info, int index) => _deserializer.ReadU32();

        ulong ITypeDeserializer.ReadU64(ISerdeInfo info, int index) => _deserializer.ReadU64();

        void ITypeDeserializer.SkipValue(ISerdeInfo info, int index) => _deserializer._argIndex++;

        DateTime ITypeDeserializer.ReadDateTime(ISerdeInfo info, int index)
            => _deserializer.ReadDateTime();

        void ITypeDeserializer.ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer)
            => _deserializer.ReadBytes(writer);
    }
}