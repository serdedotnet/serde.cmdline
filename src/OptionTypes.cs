
using System.Collections.Immutable;

namespace Serde.CmdLine;

internal record struct Option(ImmutableArray<string> FlagNames, int FieldIndex);

internal record struct SubCommand(string Name, int FieldIndex);

internal record struct CommandGroup(
    int FieldIndex,
    ISerdeInfo SerdeInfo,
    ImmutableArray<string> CommandNames
);

internal record struct Parameter(int Ordinal, int FieldIndex);

// Tracks an option from a parent command with information about which parent it belongs to
internal record struct ParentOption(
    ImmutableArray<string> FlagNames,
    int ParentDepth  // How many levels up: 1 = immediate parent, 2 = grandparent, etc.
);

internal record struct Command(
    ImmutableArray<Option> Options,
    ImmutableArray<SubCommand> SubCommands,
    ImmutableArray<CommandGroup> CommandGroups,
    ImmutableArray<Parameter> Parameters
);