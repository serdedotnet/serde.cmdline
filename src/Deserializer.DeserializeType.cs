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
            System.IO.File.AppendAllText("/tmp/debug.txt", $"TryReadIndexWithName called for {serdeInfo.Name}, argIndex={_deserializer._argIndex}, claimedCount={_deserializer._claimedArgIndices.Count}, claimed=[{string.Join(",", _deserializer._claimedArgIndices)}]\n");
            
            // First check if we have any pending parent options to process
            if (_deserializer._pendingParentOptions.TryDequeue(out var pending))
            {
                // Restore arg index to the saved position to re-read the option
                var savedArgIndex = _deserializer._argIndex;
                _deserializer._argIndex = pending.ArgIndex;
                // Process the option (it will consume the arg)
                var arg = _deserializer._args[_deserializer._argIndex];
                var (fieldIndex, errorName) = CheckFields(arg);
                return (fieldIndex, errorName);
            }

            // Skip any arguments that have been claimed by parent commands
            while (_deserializer._argIndex < _deserializer._args.Length &&
                   _deserializer._claimedArgIndices.Contains(_deserializer._argIndex))
            {
                System.IO.File.AppendAllText("/tmp/debug.txt", $"Skipping claimed arg at index {_deserializer._argIndex}: {_deserializer._args[_deserializer._argIndex]}\n");
                _deserializer._argIndex++;
            }

            if (_deserializer._argIndex == _deserializer._args.Length)
            {
                System.IO.File.AppendAllText("/tmp/debug.txt", $"Reached end of args\n");
                return (ITypeDeserializer.EndOfType, null);
            }

            var arg2 = _deserializer._args[_deserializer._argIndex];
            System.IO.File.AppendAllText("/tmp/debug.txt", $"Processing arg at index {_deserializer._argIndex}: {arg2}\n");
            while (_deserializer._handleHelp && arg2 is "-h" or "--help")
            {
                _deserializer._argIndex++;
                _deserializer._helpInfos.Add(serdeInfo);
                
                // Skip claimed arguments again
                while (_deserializer._argIndex < _deserializer._args.Length &&
                       _deserializer._claimedArgIndices.Contains(_deserializer._argIndex))
                {
                    _deserializer._argIndex++;
                }
                
                if (_deserializer._argIndex == _deserializer._args.Length)
                {
                    return (ITypeDeserializer.EndOfType, null);
                }
                arg2 = _deserializer._args[_deserializer._argIndex];
            }
            var (fieldIndex2, errorName2) = CheckFields(arg2);
            System.IO.File.AppendAllText("/tmp/debug.txt", $"CheckFields returned fieldIndex={fieldIndex2}\n");
            if (fieldIndex2 >= 0)
            {
                return (fieldIndex2, errorName2);
            }

            throw new ArgumentSyntaxException($"Unexpected argument: '{arg2}'");
        }

        int ITypeDeserializer.TryReadIndex(ISerdeInfo info)
        {
            var (fieldIndex, _) = TryReadIndexWithName(info);
            return fieldIndex;
        }

        private (int, string?) CheckFields(string arg)
        {
            System.IO.File.AppendAllText("/tmp/debug.txt", $"CheckFields called with arg='{arg}', options count={_command.Options.Length}, cmdGroups count={_command.CommandGroups.Length}\n");
            
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
                        // Before processing the subcommand, scan ahead for parent options
                        // Push current command onto parent stack so subcommands know about it
                        _deserializer._parentCommands.Push(_command);
                        
                        // Scan remaining arguments for parent options
                        ScanForParentOptions();
                        
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
            System.IO.File.AppendAllText("/tmp/debug.txt", $"CheckFields: No match found for arg='{arg}'\n");
            return (-1, null);
        }

        private void ScanForParentOptions()
        {
            // Look ahead in arguments to find any options that belong to parent commands
            // We need to mark these for processing by the parent after the subcommand is done
            var scanIndex = _deserializer._argIndex + 1; // Start after the subcommand name
            var foundOptions = new List<(int FieldIndex, int ArgIndex)>();
            
            System.IO.File.AppendAllText("/tmp/debug.txt", $"ScanForParentOptions: starting at index {scanIndex}\n");
            
            while (scanIndex < _deserializer._args.Length)
            {
                var arg = _deserializer._args[scanIndex];
                
                if (!arg.StartsWith('-'))
                {
                    // Not an option, stop scanning (could be a positional argument or nested subcommand)
                    break;
                }
                
                // Check if this option belongs to current (parent) command
                bool matchedParent = false;
                foreach (var option in _command.Options)
                {
                    foreach (var flagName in option.FlagNames)
                    {
                        if (flagName == arg)
                        {
                            // This option belongs to the parent command
                            foundOptions.Add((option.FieldIndex, scanIndex));
                            // Mark this argument index as claimed so subcommand won't try to process it
                            _deserializer._claimedArgIndices.Add(scanIndex);
                            System.IO.File.AppendAllText("/tmp/debug.txt", $"ScanForParentOptions: claimed index {scanIndex} (arg='{arg}')\n");
                            matchedParent = true;
                            break;
                        }
                    }
                    if (matchedParent) break;
                }
                
                if (matchedParent)
                {
                    // Move to next argument (skip the option value if any)
                    scanIndex++;
                    // For boolean flags, check if next arg is a boolean value
                    if (scanIndex < _deserializer._args.Length && 
                        bool.TryParse(_deserializer._args[scanIndex], out _))
                    {
                        // Also mark the boolean value as claimed
                        _deserializer._claimedArgIndices.Add(scanIndex);
                        System.IO.File.AppendAllText("/tmp/debug.txt", $"ScanForParentOptions: claimed bool value at index {scanIndex}\n");
                        scanIndex++; // Skip the boolean value
                    }
                }
                else
                {
                    // This option doesn't belong to parent, might belong to subcommand
                    // Continue scanning in case there are more parent options later
                    scanIndex++;
                }
            }
            
            System.IO.File.AppendAllText("/tmp/debug.txt", $"ScanForParentOptions: found {foundOptions.Count} parent options\n");
            
            // Queue the found parent options for processing
            foreach (var opt in foundOptions)
            {
                _deserializer._pendingParentOptions.Enqueue(opt);
            }
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