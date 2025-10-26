
using System.Collections.Immutable;

namespace Serde.CmdLine;

internal sealed record Option(ImmutableArray<string> FlagNames, int FieldIndex);

internal sealed record SubCommand(string Name, int FieldIndex);

internal sealed record CommandGroup(
    int FieldIndex,
    ISerdeInfo SerdeInfo,
    ImmutableArray<string> CommandNames
);

internal sealed record Parameter(int Ordinal, int FieldIndex);

internal sealed record Command(
    ImmutableArray<Option> Options,
    ImmutableArray<SubCommand> SubCommands,
    ImmutableArray<CommandGroup> CommandGroups,
    ImmutableArray<Parameter> Parameters
);