
A simple command line parser based on Serde.NET and Spectre.Console.

You describe the command line as types, and the parser deserializes the arguments into them.

Simple usage:

```csharp
[GenerateDeserialize]
internal sealed partial record FileSizeCommand
{
    [CommandParameter(0, "searchPath",
        Description = "Path to search. Defaults to current directory.")]
    public string? SearchPath { get; init; }

    [CommandOption("-p|--pattern")]
    public string? SearchPattern { get; init; }

    [CommandOption("--hidden")]
    public bool? IncludeHidden { get; init; }
}

if (CmdLine.TryParse<FileSizeCommand>(args, AnsiConsole.Console, out var cmd))
{
    // handle cmd
}
```

## A complete example

A small package manager with two subcommands:

```csharp
using Serde;
using Serde.CmdLine;
using Spectre.Console;

Pkg pkg;
switch (CmdLine.Parse<Pkg>(args, AnsiConsole.Console))
{
    case CmdLine.ParseResult<Pkg>.Parsed(var parsed):
        pkg = parsed;
        break;
    case CmdLine.ParseResult<Pkg>.HelpShown:
        return 0;
    default:
        // The error and the help have been printed.
        return 1;
}

switch (pkg.Command)
{
    case PkgCommand.Install install:
        foreach (var package in install.Packages)
        {
            Console.WriteLine($"Installing {package} from {install.Source}");
        }
        break;
    case PkgCommand.Remove remove:
        Console.WriteLine($"Removing {remove.Package}");
        break;
    case null:
        Console.WriteLine(CmdLine.GetHelpText<Pkg>(includeHelp: true));
        break;
}
return 0;

[GenerateDeserialize]
[Command("pkg", Summary = "A tiny package manager.")]
sealed partial record Pkg
{
    // A bool option is a flag. The initializer makes it optional.
    [CommandOption("-v|--verbose", Description = "Show detailed output.")]
    public bool Verbose { get; init; } = false;

    // One of the subcommands below, or null if none was given.
    [CommandGroup("command")]
    public PkgCommand? Command { get; init; }
}

[GenerateDeserialize]
abstract partial record PkgCommand
{
    private PkgCommand() { }

    [Command("install", Summary = "Install packages.")]
    public sealed partial record Install : PkgCommand
    {
        // An option with a value. It's required: not nullable and no initializer.
        [CommandOption("-s|--source", Description = "Feed to install from.")]
        public required string Source { get; init; }

        [CommandOption("--dry-run", Description = "Show what would be installed.")]
        public bool DryRun { get; init; } = false;

        // A collection parameter takes every remaining positional argument.
        [CommandParameter(0, "packages", Description = "Packages to install.")]
        public required List<string> Packages { get; init; }
    }

    [Command("remove", Summary = "Remove a package.")]
    public sealed partial record Remove : PkgCommand
    {
        [CommandParameter(0, "package", Description = "Package to remove.")]
        public required string Package { get; init; }
    }
}
```

`pkg install -s nuget.org a --dry-run b -v` parses to an `Install` with `Source = "nuget.org"`,
`DryRun = true` and `Packages = [a, b]`, and sets `Verbose` on `Pkg`.

`pkg --help` prints:

```
usage: pkg [-v | --verbose] [-h | --help] <command>

A tiny package manager.

Options:
    -v, --verbose  Show detailed output.
    -h, --help  Show help information.

Commands:
    install  Install packages.
    remove  Remove a package.
```

`pkg install --help` prints:

```
usage: pkg install (-s | --source <source>) [--dry-run] [-h | --help] <packages>...

Install packages.

Arguments:
    <packages>...  Packages to install.

Options:
    -s, --source  <source>  Feed to install from.
    --dry-run  Show what would be installed.
    -h, --help  Show help information.
```

This example is kept in sync with `test/Serde.CmdLine.Test/ReadmeExampleTests.cs`.

## How arguments are parsed

- **Options** (`[CommandOption]`) can appear anywhere: before or after a subcommand, and between
  the values of a collection parameter. A `bool` option is a flag; any other option takes the next
  argument as its value.
- **Parameters** (`[CommandParameter]`) are positional, in ordinal order. A collection parameter,
  such as `List<string>`, takes every remaining positional argument and must be the last one.
- **Optional or required:** a member is optional if its type is nullable or it has an initializer,
  and required otherwise. An optional collection parameter can default to empty with `= [];`.
- **Subcommands** (`[CommandGroup]`) are cases of an abstract record with a private constructor,
  each named with `[Command]`. A command can't have both parameters and subcommands.
- **`--`** ends option parsing. Every argument after it is a parameter, even if it starts with `-`.
- **`-h` and `--help`** show help for the command they appear in.

Options can't be collections yet, so `--include a --include b` isn't supported (#47).

## Hidden commands and options

Commands, parameters, options, and command groups can be marked as `Hidden` so they
are still parseable but omitted from the generated help text. This is useful for
experimental, deprecated, or internal-only functionality.

```csharp
[GenerateDeserialize]
internal sealed partial record FileSizeCommand
{
    [CommandOption("-p|--pattern")]
    public string? SearchPattern { get; init; }

    // Parses normally, but does not appear in `CmdLine.GetHelpText` output.
    [CommandOption("--experimental", Hidden = true)]
    public bool? Experimental { get; init; }
}
```
