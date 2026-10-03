using System.Collections.Generic;
using Spectre.Console.Testing;
using Xunit;

namespace Serde.CmdLine.Test;

/// <summary>
/// The example in README.md. Keep the two in sync.
/// </summary>
public sealed partial class ReadmeExampleTests
{
    [Fact]
    public void Parse()
    {
        string[] args = [ "install", "-s", "nuget.org", "a", "--dry-run", "b", "-v" ];
        Assert.True(CmdLine.TryParse<Pkg>(args, new TestConsole(), out var pkg));
        Assert.True(pkg.Verbose);
        var install = Assert.IsType<PkgCommand.Install>(pkg.Command);
        Assert.Equal("nuget.org", install.Source);
        Assert.True(install.DryRun);
        Assert.Equal([ "a", "b" ], install.Packages);
    }

    [Fact]
    public void TopLevelHelp()
    {
        var text = """
usage: pkg [-v | --verbose] [-h | --help] <command>

A tiny package manager.

Options:
    -v, --verbose  Show detailed output.
    -h, --help  Show help information.

Commands:
    install  Install packages.
    remove  Remove a package.


""";
        Assert.Equal(text.NormalizeLineEndings(), GetHelpOutput([ "--help" ]));
    }

    [Fact]
    public void SubCommandHelp()
    {
        var text = """
usage: pkg install (-s | --source <source>) [--dry-run] [-h | --help] <packages>...

Install packages.

Arguments:
    <packages>...  Packages to install.

Options:
    -s, --source  <source>  Feed to install from.
    --dry-run  Show what would be installed.
    -h, --help  Show help information.


""";
        Assert.Equal(text.NormalizeLineEndings(), GetHelpOutput([ "install", "--help" ]));
    }

    private static string GetHelpOutput(string[] args)
    {
        var console = new TestConsole().Width(120);
        Assert.False(CmdLine.TryParse<Pkg>(args, console, out _));
        return console.Output;
    }

    [GenerateDeserialize]
    [Command("pkg", Summary = "A tiny package manager.")]
    private sealed partial record Pkg
    {
        [CommandOption("-v|--verbose", Description = "Show detailed output.")]
        public bool Verbose { get; init; } = false;

        [CommandGroup("command")]
        public PkgCommand? Command { get; init; }
    }

    [GenerateDeserialize]
    private abstract partial record PkgCommand
    {
        private PkgCommand() { }

        [Command("install", Summary = "Install packages.")]
        public sealed partial record Install : PkgCommand
        {
            [CommandOption("-s|--source", Description = "Feed to install from.")]
            public required string Source { get; init; }

            [CommandOption("--dry-run", Description = "Show what would be installed.")]
            public bool DryRun { get; init; } = false;

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
}
