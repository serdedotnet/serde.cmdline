
using System.Collections.Immutable;

namespace Serde.CmdLine;

internal enum FieldKind
{
    /// <summary>
    /// No field matched
    /// </summary>
    None,
    /// <summary>
    /// An option field matched (requires incrementing arg index)
    /// </summary>
    Option,
    /// <summary>
    /// A subcommand field matched (requires incrementing arg index)
    /// </summary>
    SubCommand,
    /// <summary>
    /// A command group field matched (does not increment arg index)
    /// </summary>
    CommandGroup,
    /// <summary>
    /// A parameter field matched (requires incrementing param index)
    /// </summary>
    Parameter
}

internal record struct Option(
    ImmutableArray<string> FlagNames,
    int FieldIndex,
    bool HasArg
);

internal record struct SubCommand(string Name, int FieldIndex);

internal record struct CommandGroup(
    int FieldIndex,
    ISerdeInfo SerdeInfo,
    ImmutableArray<string> CommandNames
);

internal record struct Parameter(int Ordinal, int FieldIndex);

internal record struct Command(
    ImmutableArray<Option> Options,
    ImmutableArray<SubCommand> SubCommands,
    ImmutableArray<CommandGroup> CommandGroups,
    ImmutableArray<Parameter> Parameters
);