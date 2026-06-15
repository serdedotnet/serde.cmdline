
A simple command line parser based on Serde.NET and Spectre.Console.

Simple usage:

```
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

var console = AnsiConsole.Out;
var cmdOpt = CmdLine.TryParse<FileSizeCommand>(testArgs, console);
if (cmdOpt is {} cmd)
{
   // handle cmd
}
```

## Hidden commands and options

Commands, parameters, options, and command groups can be marked as `Hidden` so they
are still parseable but omitted from the generated help text. This is useful for
experimental, deprecated, or internal-only functionality.

```
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
