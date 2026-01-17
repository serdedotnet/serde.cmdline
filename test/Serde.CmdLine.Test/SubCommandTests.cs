
using System.IO;
using System.Linq;
using Spectre.Console.Testing;
using Xunit;

namespace Serde.CmdLine.Test;

public sealed partial class SubCommandTests
{
    [Fact]
    public void NoSubCommand()
    {
        string[] testArgs = [ "-v" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { Verbose = true, SubCommand = null }, cmd);
    }

    [Fact]
    public void FirstCommand()
    {
        string[] testArgs = [ "-v", "first" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { Verbose = true, SubCommand = new SubCommand.FirstCommand() }, cmd);
    }

    [Fact]
    public void FirstCommandOutOfOrder()
    {
        string[] testArgs = [ "first", "-v" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { Verbose = true, SubCommand = new SubCommand.FirstCommand() }, cmd);
    }

    [Fact]
    public void FirstCommandOutOfOrderStringOption()
    {
        string[] testArgs = [ "first", "-t", "value" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { StringOption = "value", SubCommand = new SubCommand.FirstCommand() }, cmd);
    }

    [Fact]
    public void FirstCommandUnknownOption()
    {
        // -x is unknown to both FirstCommand and TopCommand
        string[] testArgs = [ "first", "-x" ];
        Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRawWithHelp<TopCommand>(testArgs));
    }

    [Fact]
    public void FirstCommandWithShortOption()
    {
        string[] testArgs = [ "-v", "first", "-s" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { Verbose = true, SubCommand = new SubCommand.FirstCommand() { SomeOption = true } }, cmd);
    }

    [Fact]
    public void FirstCommandWithLongOption()
    {
        string[] testArgs = [ "-v", "first", "--some-option" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { Verbose = true, SubCommand = new SubCommand.FirstCommand() { SomeOption = true } }, cmd);
    }

    /// <summary>
    /// When the argument following an unknown option looks like a value, it gets consumed.
    /// Even if that value happens to be a sibling command name, from the subcommand's perspective
    /// it doesn't know about sibling commands.
    /// </summary>
    [Fact]
    public void SkippedOptionValueLooksLikeCommand()
    {
        // -t is unknown to SecondCommand, "first" looks like a value from SecondCommand's perspective
        // (SecondCommand doesn't know about its sibling "first" command)
        // So "first" gets consumed as the value for -t
        string[] testArgs = [ "second", "-t", "first" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { StringOption = "first", SubCommand = new SubCommand.SecondCommand() }, cmd);
    }

    /// <summary>
    /// When a skipped boolean option is followed by "true" or "false",
    /// Boolean flags should not accept explicit "true"/"false" values.
    /// The literal "true" should be treated as an unrecognized argument.
    /// </summary>
    [Fact]
    public void SkippedBoolOptionWithExplicitTrue()
    {
        string[] testArgs = [ "first", "-v", "true" ];
        Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRawWithHelp<TopCommand>(testArgs));
    }

    /// <summary>
    /// Boolean flags should not accept explicit "true"/"false" values.
    /// The literal "false" should be treated as an unrecognized argument.
    /// </summary>
    [Fact]
    public void SkippedBoolOptionWithExplicitFalse()
    {
        string[] testArgs = [ "first", "-v", "false" ];
        Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRawWithHelp<TopCommand>(testArgs));
    }

    /// <summary>
    /// When multiple options are skipped, they should all be passed to the parent.
    /// </summary>
    [Fact]
    public void MultipleSkippedOptions()
    {
        string[] testArgs = [ "first", "-v", "-t", "myvalue" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        Assert.Equal(new TopCommand { Verbose = true, StringOption = "myvalue", SubCommand = new SubCommand.FirstCommand() }, cmd);
    }

    /// <summary>
    /// A skipped string option followed by another option consumes the next token as its value,
    /// following GNU getopt behavior.
    /// </summary>
    [Fact]
    public void SkippedStringOptionFollowedByAnotherOption()
    {
        // -t expects a value, and GNU behavior consumes the next token even if it starts with '-'
        string[] testArgs = [ "first", "-t", "-v" ];
        var cmd = CmdLine.ParseRawWithHelp<TopCommand>(testArgs).Unwrap();
        // -t gets the literal value "-v", Verbose remains false
        Assert.Equal(new TopCommand { StringOption = "-v", SubCommand = new SubCommand.FirstCommand() }, cmd);
    }

    /// <summary>
    /// When a skipped boolean flag is followed by an unknown argument,
    /// it should be treated as an unrecognized argument error.
    /// </summary>
    [Fact]
    public void SkippedBoolFlagFollowedByUnknownArg()
    {
        // -v is a bool flag (recognized by TopCommand), "myfile.txt" is unknown to both
        string[] testArgs = [ "first", "-v", "myfile.txt" ];
        Assert.Throws<ArgumentSyntaxException>(() => CmdLine.ParseRawWithHelp<TopCommand>(testArgs));
    }

    [Fact]
    public void TopLevelHelp()
    {
        var help = CmdLine.GetHelpText(SerdeInfoProvider.GetDeserializeInfo<TopCommand>());
        var text = """
usage: TopCommand [-v | --verbose] [-h | --help] [-t | --string-option <stringOption>] <command>

Options:
    -v, --verbose
    -h, --help
    -t, --string-option  <stringOption>

Commands:
    first
    second

""";
        Assert.Equal(text.NormalizeLineEndings(), help.NormalizeLineEndings());
    }

    [GenerateDeserialize]
    private partial record TopCommand
    {
        [CommandOption("-v|--verbose")]
        public bool? Verbose { get; init; }

        [CommandOption("-h|--help")]
        public bool? Help { get; init; }

        [CommandOption("-t|--string-option")]
        public string? StringOption { get; init; }

        [CommandGroup("command")]
        public SubCommand? SubCommand { get; init; }
    }

    [GenerateDeserialize]
    private abstract partial record SubCommand
    {
        private SubCommand() { }

        [Command("first")]
        public sealed partial record FirstCommand : SubCommand
        {
            [CommandOption("-s|--some-option")]
            public bool? SomeOption { get; init; }
        }

        [Command("second")]
        public sealed partial record SecondCommand : SubCommand;
    }

    /// <summary>
    /// Test to verify that two command parameters in a nested subcommand are parsed correctly.
    /// This test is designed to reproduce a bug where the second parameter is parsed as a
    /// duplicate of the first parameter due to _paramIndex not being incremented.
    /// </summary>
    [Fact]
    public void NestedSubCommandWithTwoParameters()
    {
        string[] testArgs = [ "copy", "source.txt", "dest.txt" ];
        var cmd = CmdLine.ParseRawWithHelp<CommandWithParams>(testArgs).Unwrap();
        Assert.Equal(new CommandWithParams
        {
            SubCommandWithParams = new SubCommandWithParams.CopyCommand
            {
                Source = "source.txt",
                Destination = "dest.txt"
            }
        }, cmd);
    }

    [GenerateDeserialize]
    private partial record CommandWithParams
    {
        [CommandGroup("command")]
        public SubCommandWithParams? SubCommandWithParams { get; init; }
    }

    [GenerateDeserialize]
    private abstract partial record SubCommandWithParams
    {
        private SubCommandWithParams() { }

        [Command("copy")]
        public sealed partial record CopyCommand : SubCommandWithParams
        {
            [CommandParameter(0, "source")]
            public string? Source { get; init; }

            [CommandParameter(1, "destination")]
            public string? Destination { get; init; }
        }
    }
}