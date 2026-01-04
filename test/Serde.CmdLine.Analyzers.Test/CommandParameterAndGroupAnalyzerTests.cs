using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Serde.CmdLine.Analyzers.CommandParameterAndGroupAnalyzer>;

namespace Serde.CmdLine.Analyzers.Test;

public class CommandParameterAndGroupAnalyzerTests
{
    [Fact]
    public async Task NoError_WhenOnlyCommandParameter()
    {
        var source = """
            public class MyCommand
            {
                [CommandParameter(0, "file")]
                public string FilePath { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class CommandParameterAttribute : System.Attribute
            {
                public CommandParameterAttribute(int ordinal, string name) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_WhenOnlyCommandGroup()
    {
        var source = """
            public class MyCommand
            {
                [CommandGroup("command")]
                public object SubCommand { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class CommandGroupAttribute : System.Attribute
            {
                public CommandGroupAttribute(string name) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Error_WhenBothCommandParameterAndCommandGroup()
    {
        var source = """
            public class {|#0:MyCommand|}
            {
                [CommandParameter(0, "file")]
                public string FilePath { get; set; }

                [CommandGroup("command")]
                public object SubCommand { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class CommandParameterAttribute : System.Attribute
            {
                public CommandParameterAttribute(int ordinal, string name) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class CommandGroupAttribute : System.Attribute
            {
                public CommandGroupAttribute(string name) { }
            }
            """;

        var expected = Verify.Diagnostic(CommandParameterAndGroupAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("MyCommand");

        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoError_WhenAttributesOnDifferentTypes()
    {
        var source = """
            public class ParentCommand
            {
                [CommandGroup("command")]
                public ChildCommand SubCommand { get; set; }
            }

            public class ChildCommand
            {
                [CommandParameter(0, "file")]
                public string FilePath { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class CommandParameterAttribute : System.Attribute
            {
                public CommandParameterAttribute(int ordinal, string name) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class CommandGroupAttribute : System.Attribute
            {
                public CommandGroupAttribute(string name) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
