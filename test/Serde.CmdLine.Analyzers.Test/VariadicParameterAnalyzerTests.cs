using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Serde.CmdLine.Analyzers.VariadicParameterAnalyzer>;

namespace Serde.CmdLine.Analyzers.Test;

public class VariadicParameterAnalyzerTests
{
    private const string Attribute = """

        [System.AttributeUsage(System.AttributeTargets.Property)]
        public class CommandParameterAttribute : System.Attribute
        {
            public CommandParameterAttribute(int ordinal, string name) { }
        }
        """;

    [Fact]
    public async Task NoError_WhenCollectionIsLast()
    {
        var source = """
            public class MyCommand
            {
                [CommandParameter(0, "dest")]
                public string Dest { get; set; }

                [CommandParameter(1, "files")]
                public string[] Files { get; set; }
            }
            """ + Attribute;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_WhenStringIsNotLast()
    {
        // A string is IEnumerable<char>, but it isn't a collection parameter.
        var source = """
            public class MyCommand
            {
                [CommandParameter(0, "name")]
                public string Name { get; set; }

                [CommandParameter(1, "dest")]
                public string Dest { get; set; }
            }
            """ + Attribute;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_WhenCollectionHasLastOrdinalButIsDeclaredFirst()
    {
        var source = """
            using System.Collections.Generic;

            public class MyCommand
            {
                [CommandParameter(1, "files")]
                public List<string> Files { get; set; }

                [CommandParameter(0, "dest")]
                public string Dest { get; set; }
            }
            """ + Attribute;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Error_WhenCollectionIsDeclaredLastButHasEarlierOrdinal()
    {
        var source = """
            public class MyCommand
            {
                [CommandParameter(1, "dest")]
                public string Dest { get; set; }

                [CommandParameter(0, "files")]
                public string[] {|#0:Files|} { get; set; }
            }
            """ + Attribute;

        var expected = Verify.Diagnostic(VariadicParameterAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Files");
        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task Error_WhenCollectionIsNotLast()
    {
        var source = """
            using System.Collections.Generic;

            public class MyCommand
            {
                [CommandParameter(0, "files")]
                public List<string> {|#0:Files|} { get; set; }

                [CommandParameter(1, "dest")]
                public string Dest { get; set; }
            }
            """ + Attribute;

        var expected = Verify.Diagnostic(VariadicParameterAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Files");
        await Verify.VerifyAnalyzerAsync(source, expected);
    }
}
