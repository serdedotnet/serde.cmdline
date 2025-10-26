using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Serde.CmdLine.Benchmarks;

[MemoryDiagnoser]
public partial class CmdLineBenchmarks
{
    private string[] _simpleArgs = null!;
    private string[] _complexArgs = null!;
    private string[] _subCommandArgs = null!;
    private string[] _helpArgs = null!;
    private string[] _manyOptionsArgs = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleArgs = ["-f", "value"];
        _complexArgs = ["-p", "*.txt", "--hidden", "/some/path"];
        _subCommandArgs = ["-v", "first", "--some-option"];
        _helpArgs = ["-h"];
        _manyOptionsArgs = ["-a", "val1", "-b", "val2", "-c", "val3", "-d", "val4", "-e", "val5"];
    }

    [Benchmark]
    public SimpleCommand ParseSimpleCommand()
    {
        return CmdLine.ParseRaw<SimpleCommand>(_simpleArgs);
    }

    [Benchmark]
    public ComplexCommand ParseComplexCommand()
    {
        return CmdLine.ParseRaw<ComplexCommand>(_complexArgs);
    }

    [Benchmark]
    public TopCommand ParseSubCommand()
    {
        return CmdLine.ParseRaw<TopCommand>(_subCommandArgs);
    }

    [Benchmark]
    public CmdLine.ParsedArgsOrHelpInfos<ComplexCommand> ParseWithHelp()
    {
        return CmdLine.ParseRawWithHelp<ComplexCommand>(_helpArgs);
    }

    [Benchmark]
    public ManyOptionsCommand ParseManyOptions()
    {
        return CmdLine.ParseRaw<ManyOptionsCommand>(_manyOptionsArgs);
    }

    [Benchmark]
    public string GenerateHelpText()
    {
        return CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<ComplexCommand>());
    }

    [Benchmark]
    public string GenerateSubCommandHelpText()
    {
        return CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<TopCommand>());
    }

    // Simple command with minimal options
    [GenerateDeserialize]
    public sealed partial record SimpleCommand
    {
        [CommandOption("-f|--flag")]
        public bool? Flag { get; init; }

        [CommandParameter(0, "value")]
        public required string Value { get; init; }
    }

    // Complex command with multiple option types
    [GenerateDeserialize]
    public sealed partial record ComplexCommand
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

    // Command with subcommands
    [GenerateDeserialize]
    public partial record TopCommand
    {
        [CommandOption("-v|--verbose")]
        public bool? Verbose { get; init; }

        [CommandOption("-h|--help")]
        public bool? Help { get; init; }

        [CommandGroup("command")]
        public SubCommand? SubCommand { get; init; }
    }

    [GenerateDeserialize]
    public abstract partial record SubCommand
    {
        private SubCommand() { }

        [Command("first")]
        public sealed partial record FirstCommand : SubCommand
        {
            [CommandOption("-s|--some-option")]
            public bool? SomeOption { get; init; }
        }

        [Command("second")]
        public sealed partial record SecondCommand : SubCommand
        {
            [CommandParameter(0, "arg")]
            public string? Arg { get; init; }
        }
    }

    // Command with many options
    [GenerateDeserialize]
    public sealed partial record ManyOptionsCommand
    {
        [CommandOption("-a|--option-a")]
        public string? OptionA { get; init; }

        [CommandOption("-b|--option-b")]
        public string? OptionB { get; init; }

        [CommandOption("-c|--option-c")]
        public string? OptionC { get; init; }

        [CommandOption("-d|--option-d")]
        public string? OptionD { get; init; }

        [CommandOption("-e|--option-e")]
        public string? OptionE { get; init; }

        [CommandOption("-f|--option-f")]
        public string? OptionF { get; init; }

        [CommandOption("-g|--option-g")]
        public string? OptionG { get; init; }

        [CommandOption("-h|--option-h")]
        public string? OptionH { get; init; }
    }
}
