using System;
using System.Collections.Generic;
using Spectre.Console.Testing;
using Xunit;

namespace Serde.CmdLine.Test;

public sealed partial class VariadicTests
{
    [Fact]
    public void CollectsRemainingParameters()
    {
        string[] testArgs = [ "dest", "a.txt", "b.txt", "c.txt" ];
        var cmd = CmdLine.ParseRaw<CopyCommand>(testArgs);
        Assert.Equal("dest", cmd.Dest);
        Assert.Equal([ "a.txt", "b.txt", "c.txt" ], cmd.Files);
    }

    [Fact]
    public void NoValuesIsNull()
    {
        string[] testArgs = [ "dest" ];
        var cmd = CmdLine.ParseRaw<CopyCommand>(testArgs);
        Assert.Null(cmd.Files);
    }

    [Fact]
    public void OptionsBetweenValues()
    {
        string[] testArgs = [ "dest", "a.txt", "-v", "b.txt", "-o", "out", "c.txt" ];
        var cmd = CmdLine.ParseRaw<CopyCommand>(testArgs);
        Assert.True(cmd.Verbose);
        Assert.Equal("out", cmd.Output);
        Assert.Equal([ "a.txt", "b.txt", "c.txt" ], cmd.Files);
    }

    [Fact]
    public void EndOfOptionsInsideValues()
    {
        string[] testArgs = [ "dest", "a.txt", "--", "-v", "--help", "--" ];
        var cmd = CmdLine.ParseRawWithHelp<CopyCommand>(testArgs).Unwrap();
        Assert.Null(cmd.Verbose);
        Assert.Equal([ "a.txt", "-v", "--help", "--" ], cmd.Files);
    }

    [Fact]
    public void EndOfOptionsBeforeParameters()
    {
        string[] testArgs = [ "-v", "--", "-dest", "-a.txt" ];
        var cmd = CmdLine.ParseRaw<CopyCommand>(testArgs);
        Assert.True(cmd.Verbose);
        Assert.Equal("-dest", cmd.Dest);
        Assert.Equal([ "-a.txt" ], cmd.Files);
    }

    [Fact]
    public void HelpBetweenValues()
    {
        string[] testArgs = [ "dest", "a.txt", "--help", "b.txt" ];
        var result = CmdLine.ParseRawWithHelp<CopyCommand>(testArgs);
        Assert.IsType<CmdLine.ParsedArgsOrHelpInfos<CopyCommand>.Help>(result);
    }

    [Fact]
    public void UnknownOptionBetweenValues()
    {
        string[] testArgs = [ "dest", "a.txt", "-x", "b.txt" ];
        var ex = Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRaw<CopyCommand>(testArgs));
        Assert.Equal("Unexpected argument: '-x'", ex.Message);
    }

    [Fact]
    public void UnknownOptionAfterSkippedOption()
    {
        // -v is held back for CopyCommand when -x fails. The error must name -x, not -v.
        string[] testArgs = [ "dest", "a.txt", "-v", "-x" ];
        var ex = Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRaw<CopyCommand>(testArgs));
        Assert.Equal("Unexpected argument: '-x'", ex.Message);
    }

    [Fact]
    public void OptionMissingValueBetweenValues()
    {
        string[] testArgs = [ "dest", "a.txt", "-o" ];
        var ex = Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRaw<CopyCommand>(testArgs));
        Assert.Equal("Option '-o' requires a value.", ex.Message);
    }

    [Fact]
    public void ParsesElementType()
    {
        string[] testArgs = [ "1", "2", "3" ];
        var cmd = CmdLine.ParseRaw<SumCommand>(testArgs);
        Assert.Equal([ 1, 2, 3 ], cmd.Numbers);
    }

    [Fact]
    public void RequiredCollectionMissing()
    {
        Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRaw<SumCommand>([]));
    }

    [Fact]
    public void SubCommandWithParentOptionBetweenValues()
    {
        string[] testArgs = [ "add", "a.txt", "-v", "b.txt" ];
        var cmd = CmdLine.ParseRaw<TopCommand>(testArgs);
        Assert.True(cmd.Verbose);
        var add = Assert.IsType<SubCommand.Add>(cmd.SubCommand);
        Assert.Equal([ "a.txt", "b.txt" ], add.Files);
        Assert.False(add.Force);
    }

    [Fact]
    public void SubCommandWithOwnOptionBetweenValues()
    {
        string[] testArgs = [ "add", "a.txt", "-f", "b.txt" ];
        var cmd = CmdLine.ParseRaw<TopCommand>(testArgs);
        var add = Assert.IsType<SubCommand.Add>(cmd.SubCommand);
        Assert.Equal([ "a.txt", "b.txt" ], add.Files);
        Assert.True(add.Force);
    }

    [Fact]
    public void CollectionOptionIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => CmdLine.ParseRaw<CollectionOptionCommand>([ "-i", "a" ]));
    }

    [Fact]
    public void HelpShowsVariadicParameter()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<SumCommand>());
        var text = """
usage: SumCommand <numbers>...

Arguments:
    <numbers>...

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [GenerateDeserialize]
    private sealed partial record CopyCommand
    {
        [CommandOption("-v|--verbose")]
        public bool? Verbose { get; init; }

        [CommandOption("-o|--output")]
        public string? Output { get; init; }

        [CommandParameter(0, "dest")]
        public required string Dest { get; init; }

        [CommandParameter(1, "files")]
        public List<string>? Files { get; init; }
    }

    [GenerateDeserialize]
    private sealed partial record SumCommand
    {
        [CommandParameter(0, "numbers")]
        public required List<int> Numbers { get; init; }
    }

    [GenerateDeserialize]
    private sealed partial record CollectionOptionCommand
    {
        [CommandOption("-i|--include")]
        public List<string>? Include { get; init; }
    }

    [GenerateDeserialize]
    private sealed partial record TopCommand
    {
        [CommandOption("-v|--verbose")]
        public bool? Verbose { get; init; }

        [CommandGroup("command")]
        public SubCommand? SubCommand { get; init; }
    }

    [GenerateDeserialize]
    private abstract partial record SubCommand
    {
        private SubCommand() { }

        [Command("add")]
        public sealed partial record Add : SubCommand
        {
            [CommandOption("-f|--force")]
            public bool Force { get; init; } = false;

            [CommandParameter(0, "files")]
            public required List<string> Files { get; init; }
        }
    }
}
