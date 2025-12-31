using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Serde.CmdLine.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandParameterAndGroupAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SERDECMD001";

    private static readonly LocalizableString Title =
        "CommandParameter and CommandGroup cannot be used together";

    private static readonly LocalizableString MessageFormat =
        "Type '{0}' has both [CommandParameter] and [CommandGroup] attributes. These cannot be combined on the same type.";

    private static readonly LocalizableString Description =
        "A command type cannot have both positional parameters and subcommands. Move parameters to the subcommand types instead.";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        bool hasCommandParameter = false;
        bool hasCommandGroup = false;

        foreach (var member in namedType.GetMembers())
        {
            if (!(member is IPropertySymbol || member is IFieldSymbol))
                continue;

            foreach (var attribute in member.GetAttributes())
            {
                var attrName = attribute.AttributeClass?.Name;

                if (attrName == "CommandParameterAttribute" || attrName == "CommandParameter")
                {
                    hasCommandParameter = true;
                }
                else if (attrName == "CommandGroupAttribute" || attrName == "CommandGroup")
                {
                    hasCommandGroup = true;
                }
            }
        }

        if (hasCommandParameter && hasCommandGroup)
        {
            // Report on the type itself
            var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
            var diagnostic = Diagnostic.Create(Rule, location, namedType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
