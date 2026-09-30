using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console.Testing;
using Xunit;

namespace Serde.CmdLine.Test;

public sealed partial class DeserializerTests
{
    [Fact]
    public void SpectreExample()
    {
        string[] testArgs = [ "-p", "*.txt", "--hidden", ];
        var cmd = CmdLine.ParseRawWithHelp<FileSizeCommand>(testArgs).Unwrap();
        Assert.Equal(new FileSizeCommand { SearchPath = null, SearchPattern = "*.txt", IncludeHidden = true }, cmd);
    }

    [Fact]
    public void TestSearchPath()
    {
        string[] testArgs = [ "search-path" ];
        var cmd = CmdLine.ParseRawWithHelp<FileSizeCommand>(testArgs).Unwrap();
        Assert.Equal(new FileSizeCommand { SearchPath = "search-path", SearchPattern = null, IncludeHidden = null }, cmd);
    }

    [Fact]
    public void TestHelp()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<FileSizeCommand>());
        var text = """
usage: FileSizeCommand [-p | --pattern <searchPattern>] [--hidden] [-h | --help] <searchPath>

Arguments:
    <searchPath>  Path to search. Defaults to current directory.

Options:
    -p, --pattern  <searchPattern>
    --hidden
    -h, --help

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [Fact]
    public void HelpAndBadOption()
    {
        string[] args = [ "-h", "--bad-option" ];
        var testConsole = new TestConsole();
        Assert.False(CmdLine.TryParse<FileSizeCommand>(args, testConsole, out _));
        var text = """
error: Unexpected argument: '--bad-option'
usage: FileSizeCommand [-p | --pattern <searchPattern>] [--hidden] [-h | --help]
<searchPath>

Arguments:
    <searchPath>  Path to search. Defaults to current directory.

Options:
    -p, --pattern  <searchPattern>
    --hidden
    -h, --help


""";
        Assert.Equal(text.NormalizeLineEndings(), testConsole.Output);
    }

    [Fact]
    public void BadOption()
    {
        string[] args = [ "--bad-option" ];
        var testConsole = new TestConsole();
        var ex = Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRaw<FileSizeCommand>(args));
        Assert.False(CmdLine.TryParse<FileSizeCommand>(args, testConsole, out _));
        Assert.Contains(ex.Message.NormalizeLineEndings(), testConsole.Output);
    }

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

    [GenerateDeserialize]
    internal sealed partial record HiddenMembersCommand
    {
        [CommandParameter(0, "visibleArg", Description = "A visible argument.")]
        public string? VisibleArg { get; init; }

        [CommandParameter(1, "secretArg", Hidden = true)]
        public string? SecretArg { get; init; }

        [CommandOption("--visible")]
        public bool? Visible { get; init; }

        [CommandOption("--secret", Hidden = true)]
        public bool? Secret { get; init; }
    }

    [Fact]
    public void HiddenMembersStillParse()
    {
        string[] testArgs = [ "--secret", "firstArg", "secondArg" ];
        var cmd = CmdLine.ParseRawWithHelp<HiddenMembersCommand>(testArgs).Unwrap();
        Assert.Equal(new HiddenMembersCommand
        {
            VisibleArg = "firstArg",
            SecretArg = "secondArg",
            Visible = null,
            Secret = true
        }, cmd);
    }

    [Fact]
    public void HiddenMembersNotInHelp()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<HiddenMembersCommand>());
        var text = """
usage: HiddenMembersCommand [--visible] <visibleArg>

Arguments:
    <visibleArg>  A visible argument.

Options:
    --visible

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [Fact]
    public void BasicCommandTest()
    {
        string[] cmdLine = [ "-f", "abc" ];
        var cmd = CmdLine.ParseRawWithHelp<BasicCommand>(cmdLine).Unwrap();
        Assert.Equal(new BasicCommand
        {
            FlagOption = true,
            Arg = "abc"
        }, cmd);
    }

    [GenerateSerde]
    private sealed partial record BasicCommand
    {
        [CommandOption("-f|--flag-option")]
        public bool? FlagOption { get; init; }

        [CommandParameter(0, "arg")]
        public required string Arg { get; init; }
    }

    [Fact]
    public void NumericOptionsTest()
    {
        string[] cmdLine = [ "--count", "42", "--ratio", "1.5", "--big", "9000000000", "9" ];
        var cmd = CmdLine.ParseRawWithHelp<NumericCommand>(cmdLine).Unwrap();
        Assert.Equal(new NumericCommand
        {
            Count = 42,
            Ratio = 1.5,
            Big = 9_000_000_000L,
            Ordinal = 9,
        }, cmd);
    }

    [GenerateDeserialize]
    private sealed partial record NumericCommand
    {
        [CommandOption("--count")]
        public int? Count { get; init; }

        [CommandOption("--ratio")]
        public double? Ratio { get; init; }

        [CommandOption("--big")]
        public long? Big { get; init; }

        [CommandParameter(0, "ordinal")]
        public required int Ordinal { get; init; }
    }

    [Fact]
    public void BoolWithInitializerOmittedUsesDefault()
    {
        string[] cmdLine = [ "abc" ];
        var cmd = CmdLine.ParseRawWithHelp<InitializerCommand>(cmdLine).Unwrap();
        Assert.Equal(new InitializerCommand
        {
            FlagOption = false,
            Arg = "abc"
        }, cmd);
    }

    [Fact]
    public void BoolWithInitializerProvidedOverridesDefault()
    {
        string[] cmdLine = [ "-f", "abc" ];
        var cmd = CmdLine.ParseRawWithHelp<InitializerCommand>(cmdLine).Unwrap();
        Assert.Equal(new InitializerCommand
        {
            FlagOption = true,
            Arg = "abc"
        }, cmd);
    }

    [Fact]
    public void BoolWithInitializerFlagInHelp()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<InitializerCommand>());
        var text = """
usage: InitializerCommand [-f | --flag-option] <arg>

Arguments:
    <arg>

Options:
    -f, --flag-option

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [GenerateDeserialize]
    private sealed partial record InitializerCommand
    {
        [CommandOption("-f|--flag-option")]
        public bool FlagOption { get; init; } = false;

        [CommandParameter(0, "arg")]
        public required string Arg { get; init; }
    }

    [Fact]
    public void BoolWithoutInitializerIsRequired()
    {
        string[] cmdLine = [ "abc" ];
        Assert.Throws<ArgumentSyntaxException>(
            () => CmdLine.ParseRawWithHelp<RequiredBoolCommand>(cmdLine).Unwrap());
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<RequiredBoolCommand>());
        Assert.Contains("usage: RequiredBoolCommand (-f | --flag-option) <arg>", help);
    }

    [Fact]
    public void BoolWithoutInitializerProvided()
    {
        string[] cmdLine = [ "-f", "abc" ];
        var cmd = CmdLine.ParseRawWithHelp<RequiredBoolCommand>(cmdLine).Unwrap();
        Assert.Equal(new RequiredBoolCommand
        {
            FlagOption = true,
            Arg = "abc"
        }, cmd);
    }

    [GenerateDeserialize]
    private sealed partial record RequiredBoolCommand
    {
        [CommandOption("-f|--flag-option")]
        public bool FlagOption { get; init; }

        [CommandParameter(0, "arg")]
        public required string Arg { get; init; }
    }

    [Fact]
    public void StringWithInitializerOmittedUsesDefault()
    {
        string[] cmdLine = [ "abc" ];
        var cmd = CmdLine.ParseRawWithHelp<StringInitializerCommand>(cmdLine).Unwrap();
        Assert.Equal(new StringInitializerCommand
        {
            Name = "default",
            Arg = "abc"
        }, cmd);
    }

    [Fact]
    public void StringWithInitializerProvidedOverridesDefault()
    {
        string[] cmdLine = [ "-n", "custom", "abc" ];
        var cmd = CmdLine.ParseRawWithHelp<StringInitializerCommand>(cmdLine).Unwrap();
        Assert.Equal(new StringInitializerCommand
        {
            Name = "custom",
            Arg = "abc"
        }, cmd);
    }

    [GenerateDeserialize]
    private sealed partial record StringInitializerCommand
    {
        [CommandOption("-n|--name")]
        public string Name { get; init; } = "default";

        [CommandParameter(0, "arg")]
        public required string Arg { get; init; }
    }

    [Fact]
    public void RequiredOptionsNotBracketedInUsage()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<RequiredOptionCommand>());
        var text = """
usage: RequiredOptionCommand --name <name> (-o | --output <output>) [-v | --verbose]

Options:
    --name  <name>
    -o, --output  <output>
    -v, --verbose

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [GenerateDeserialize]
    private sealed partial record RequiredOptionCommand
    {
        [CommandOption("--name")]
        public required string Name { get; init; }

        [CommandOption("-o|--output")]
        public required string Output { get; init; }

        [CommandOption("-v|--verbose")]
        public bool? Verbose { get; init; }
    }

    [Fact]
    public void OptionOptionalityMatchesSerde()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<OptionOptionalityCommand>());
        Assert.Contains(
            "usage: OptionOptionalityCommand --count <count> [--name <name>] [--default <default>]",
            help);

        Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRaw<OptionOptionalityCommand>([]));
        Assert.Equal(new OptionOptionalityCommand { Count = 1 },
            CmdLine.ParseRaw<OptionOptionalityCommand>(["--count", "1"]));
    }

    [GenerateDeserialize]
    private sealed partial record OptionOptionalityCommand
    {
        [CommandOption("--count")]
        public int Count { get; init; }

        [CommandOption("--name")]
        public string? Name { get; init; }

        [CommandOption("--default")]
        public string Default { get; init; } = "default";
    }

    [Fact]
    public void DeclaredHelpOptionNotDuplicated()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<FileSizeCommand>(), includeHelp: true);
        var text = """
usage: FileSizeCommand [-p | --pattern <searchPattern>] [--hidden] [-h | --help] <searchPath>

Arguments:
    <searchPath>  Path to search. Defaults to current directory.

Options:
    -p, --pattern  <searchPattern>
    --hidden
    -h, --help

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [Fact]
    public void BuiltInHelpOptionAdded()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<HiddenMembersCommand>(), includeHelp: true);
        var text = """
usage: HiddenMembersCommand [--visible] [-h | --help] <visibleArg>

Arguments:
    <visibleArg>  A visible argument.

Options:
    --visible
    -h, --help  Show help information.

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }
}
