using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace Serde.CmdLine;

internal sealed partial class Deserializer
{
    private sealed class DeserializeType(
        Deserializer deserializer,
        Command _command
    ) : TypeDeserializerBase(deserializer)
    {
        private readonly List<string> _skippedOptions = new();

        public override void End(ISerdeInfo info)
        {
            // Pop the command stack
            _deserializer._commandStack.RemoveAt(_deserializer._commandStack.Count - 1);
            _deserializer._checkingSkipped = false;
        }

        public override int TryReadIndex(ISerdeInfo serdeInfo)
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

                if (_deserializer.TryConsumeSpecialArg(serdeInfo))
                {
                    continue;
                }

                var arg = args[argIndex];
                var (fieldIndex, fieldKind) = CheckFields(arg);
                if (fieldIndex >= 0)
                {
                    // Increment indices based on field type
                    switch (fieldKind)
                    {
                        case FieldKind.Option:
                        case FieldKind.SubCommand:
                            argIndex++;
                            break;
                        case FieldKind.Parameter:
                            _deserializer._paramIndex++;
                            break;
                    }
                    return fieldIndex;
                }

                // No match, so check parent options. We can't parse the value yet because the
                // field belongs to the parent type, so record it for the parent to read later.
                // N.B. The top of the stack is the current command
                if (_deserializer.IsOption(arg)
                    && _deserializer.TrySkipOption(_deserializer._commandStack.Count - 2, _skippedOptions))
                {
                    continue;
                }

                // Unrecognized argument
                throw new ArgumentSyntaxException($"Unexpected argument: '{arg}'");
            }
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

        private (int fieldIndex, FieldKind fieldKind) CheckFields(string arg)
        {
            var cmd = _command;
            if (_deserializer._endOfOptions)
            {
                // After "--", every arg is a parameter
                return CheckParameters(cmd);
            }

            if (_deserializer.IsOption(arg))
            {
                var fieldIndex = CheckOptions(cmd, arg)?.FieldIndex ?? -1;
                return (fieldIndex, fieldIndex >= 0 ? FieldKind.Option : FieldKind.None);
            }

            // Check for command group matches
            foreach (var subCmd in cmd.SubCommands)
            {
                if (arg == subCmd.Name)
                {
                    return (subCmd.FieldIndex, FieldKind.SubCommand);
                }
            }

            foreach (var cmdGroup in cmd.CommandGroups)
            {
                foreach (var name in cmdGroup.CommandNames)
                {
                    if (name == arg)
                    {
                        return (cmdGroup.FieldIndex, FieldKind.CommandGroup);
                    }
                }

                // No match, so we can continue.
            }

            return CheckParameters(cmd);
        }

        private (int fieldIndex, FieldKind fieldKind) CheckParameters(Command cmd)
        {
            foreach (var param in cmd.Parameters)
            {
                // Parameters are positional, so we check the current param index
                if (_deserializer._paramIndex == param.Ordinal)
                {
                    return (param.FieldIndex, FieldKind.Parameter);
                }
            }
            return (-1, FieldKind.None);
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
                        if (fieldInfo.Kind == InfoKind.List)
                        {
                            throw new InvalidOperationException(
                                $"Option '{flagNames}' is a collection. Only parameters can be collections."
                            );
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
                serdeInfo,
                options.ToImmutable(),
                subCmdNames.ToImmutable(),
                cmdGroups.ToImmutable(),
                parameters.ToImmutable()
            );
        }
    }
}