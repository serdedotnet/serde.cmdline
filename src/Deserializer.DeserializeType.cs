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
        private readonly Command _command = ParseCommandAndValidate(serdeInfo, _deserializer._parentCommands);

        (int, string?) ITypeDeserializer.TryReadIndexWithName(ISerdeInfo serdeInfo) => TryReadIndexWithName(serdeInfo);

        private (int, string?) TryReadIndexWithName(ISerdeInfo serdeInfo)
        {
            // Skip any parent options (leave them for parent to process)
            while (_deserializer._argIndex < _deserializer._args.Length)
            {
                var arg = _deserializer._args[_deserializer._argIndex];
                
                // Handle help
                if (_deserializer._handleHelp && arg is "-h" or "--help")
                {
                    _deserializer._argIndex++;
                    _deserializer._helpInfos.Add(serdeInfo);
                    continue;
                }
                
                // If this is a parent option, skip it and save for later
                if (arg.StartsWith('-'))
                {
                    var (isParent, fieldIndex, parentCmd) = FindParentOption(arg);
                    if (isParent)
                    {
                        // Save this option for the parent to process later
                        var currentArgIndex = _deserializer._argIndex;
                        _deserializer._argIndex++; // Skip the option
                        // Check if next arg might be a value for this option (boolean value)
                        if (_deserializer._argIndex < _deserializer._args.Length &&
                            bool.TryParse(_deserializer._args[_deserializer._argIndex], out _))
                        {
                            _deserializer._argIndex++; // Skip the value too
                        }
                        // Add to the parent's skipped list
                        // Find the entry in parentOptionsToProcess that matches this parent command
                        foreach (var entry in _deserializer._parentOptionsToProcess)
                        {
                            if (entry.ParentCmd.Options == parentCmd.Options) // Simple equality check
                            {
                                entry.SkippedOptions.Add((fieldIndex, currentArgIndex));
                                break;
                            }
                        }
                        continue; // Continue looking for non-parent options
                    }
                }
                
                // Not a parent option, try to match it
                break;
            }
            
            if (_deserializer._argIndex == _deserializer._args.Length)
            {
                // Check if there are skipped parent options to process for THIS command
                if (_deserializer._parentOptionsToProcess.Count > 0)
                {
                    var (parentCmd, skippedList) = _deserializer._parentOptionsToProcess.Peek();
                    // Only process if this matches our command
                    if (parentCmd.Options == _command.Options && skippedList.Count > 0)
                    {
                        var (fieldIndex, argIdx) = skippedList[0];
                        skippedList.RemoveAt(0);
                        // Set argIndex to the saved position and consume the option
                        var savedIndex = _deserializer._argIndex;
                        _deserializer._argIndex = argIdx;
                        _deserializer._argIndex++; // Consume the option
                        // Check if next is a boolean value
                        if (_deserializer._argIndex < _deserializer._args.Length &&
                            bool.TryParse(_deserializer._args[_deserializer._argIndex], out _))
                        {
                            _deserializer._argIndex++; // Consume the value
                        }
                        // If this was the last skipped option for this level, pop both stacks
                        if (skippedList.Count == 0)
                        {
                            _deserializer._parentOptionsToProcess.Pop();
                            _deserializer._parentCommands.Pop();
                        }
                        // Restore argIndex to continue from where we were
                        _deserializer._argIndex = savedIndex;
                        return (fieldIndex, null);
                    }
                }
                
                return (ITypeDeserializer.EndOfType, null);
            }

            var currentArg = _deserializer._args[_deserializer._argIndex];
            var (matchedFieldIndex, errorName) = CheckFields(currentArg);
            if (matchedFieldIndex >= 0)
            {
                return (matchedFieldIndex, errorName);
            }

            throw new ArgumentSyntaxException($"Unexpected argument: '{currentArg}'");
        }

        private bool IsParentOption(string arg)
        {
            return FindParentOption(arg).IsParent;
        }

        private (bool IsParent, int FieldIndex, Command ParentCmd) FindParentOption(string arg)
        {
            foreach (var parentCmd in _deserializer._parentCommands)
            {
                foreach (var option in parentCmd.Options)
                {
                    foreach (var flagName in option.FlagNames)
                    {
                        if (flagName == arg)
                        {
                            return (true, option.FieldIndex, parentCmd);
                        }
                    }
                }
            }
            return (false, -1, default);
        }

        int ITypeDeserializer.TryReadIndex(ISerdeInfo info)
        {
            var (fieldIndex, _) = TryReadIndexWithName(info);
            return fieldIndex;
        }

        private (int, string?) CheckFields(string arg)
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
                            _deserializer._argIndex++;
                            return (option.FieldIndex, null);
                        }
                    }
                }
                // No option match, return missing
                return (-1, null);
            }

            // Check for command group matches
            foreach (var subCmd in cmd.SubCommands)
            {
                if (arg == subCmd.Name)
                {
                    _deserializer._argIndex++;
                    return (subCmd.FieldIndex, null);
                }
            }

            foreach (var cmdGroup in cmd.CommandGroups)
            {
                foreach (var name in cmdGroup.CommandNames)
                {
                    if (name == arg)
                    {
                        // Found a command group match
                        // Push current command onto parent stack so subcommands know about it
                        _deserializer._parentCommands.Push(_command);
                        // Push the current command and a new empty list to track options skipped by the subcommand
                        _deserializer._parentOptionsToProcess.Push((_command, new List<(int, int)>()));
                        
                        return (cmdGroup.FieldIndex, null);
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
                    _deserializer._paramIndex++;
                    return (param.FieldIndex, null);
                }
            }
            return (-1, null);
        }

        private static Command ParseCommandAndValidate(ISerdeInfo serdeInfo, Stack<Command> parentCommands)
        {
            var command = ParseCommand(serdeInfo);
            
            // Validate that no options in current command conflict with parent commands
            foreach (var parentCmd in parentCommands)
            {
                foreach (var currentOption in command.Options)
                {
                    foreach (var currentFlag in currentOption.FlagNames)
                    {
                        foreach (var parentOption in parentCmd.Options)
                        {
                            foreach (var parentFlag in parentOption.FlagNames)
                            {
                                if (currentFlag == parentFlag)
                                {
                                    throw new ArgumentSyntaxException(
                                        $"Option '{currentFlag}' is defined in both parent command and subcommand. " +
                                        "Options cannot be shared between a command and its subcommands.");
                                }
                            }
                        }
                    }
                }
            }
            
            return command;
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