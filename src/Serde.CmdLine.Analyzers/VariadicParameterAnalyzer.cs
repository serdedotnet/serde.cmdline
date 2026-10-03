using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Serde.CmdLine.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VariadicParameterAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SERDECMD002";

    private static readonly LocalizableString Title =
        "A collection parameter must be the last parameter";

    private static readonly LocalizableString MessageFormat =
        "Parameter '{0}' is a collection but is not the last parameter";

    private static readonly LocalizableString Description =
        "A collection parameter takes every remaining positional argument, so no parameter can follow it.";

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

        var parameters = new List<(ISymbol Member, ITypeSymbol Type, int Ordinal)>();
        foreach (var member in namedType.GetMembers())
        {
            var type = member switch
            {
                IPropertySymbol p => p.Type,
                IFieldSymbol f => f.Type,
                _ => null
            };
            if (type is null)
                continue;

            foreach (var attribute in member.GetAttributes())
            {
                var attrName = attribute.AttributeClass?.Name;
                if ((attrName == "CommandParameterAttribute" || attrName == "CommandParameter")
                    && attribute.ConstructorArguments.Length > 0
                    && attribute.ConstructorArguments[0].Value is int ordinal)
                {
                    parameters.Add((member, type, ordinal));
                }
            }
        }

        if (parameters.Count == 0)
            return;

        int lastOrdinal = parameters.Max(p => p.Ordinal);
        foreach (var (member, type, ordinal) in parameters)
        {
            if (ordinal != lastOrdinal && IsCollection(type))
            {
                var location = member.Locations.Length > 0 ? member.Locations[0] : Location.None;
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, member.Name));
            }
        }
    }

    private static bool IsCollection(ITypeSymbol type)
    {
        // A string is enumerable but is read as a single value.
        if (type.SpecialType == SpecialType.System_String)
            return false;

        return type is IArrayTypeSymbol
            || type.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable);
    }
}
