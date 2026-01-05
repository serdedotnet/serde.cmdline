
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

    [CommandOption("-h|--help")]
    public bool? Help { get; init; }
}

var console = AnsiConsole.Out;
var cmdOpt = CmdLine.TryParse<FileSizeCommand>(testArgs, console);
if (cmdOpt is {} cmd)
{
   // handle cmd
}
```
