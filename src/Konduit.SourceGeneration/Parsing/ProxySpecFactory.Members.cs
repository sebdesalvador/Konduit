using System.Collections.Generic;
using System.Linq;
using Konduit.SourceGeneration.Model;
using Microsoft.CodeAnalysis;

namespace Konduit.SourceGeneration.Parsing;

internal static partial class ProxySpecFactory
{
    private static MethodSpec CreateMethod(
        IMethodSymbol method,
        int ordinal,
        INamedTypeSymbol serviceInterface,
        INamedTypeSymbol? implementation,
        KnownTypes known,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        var skipped = HasSkipAttribute(method, known)
            || HasSkipAttribute(implementation?.FindImplementationForInterfaceMember(method), known);

        var blocker = DescribeInterceptionBlocker(method);

        if (blocker is not null && !skipped)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                KonduitDiagnostics.MethodBypassesPipeline,
                method.Locations.FirstOrDefault() ?? location,
                $"{serviceInterface.ToDisplayString()}.{method.Name}",
                blocker));
        }

        var (kind, awaited) = ClassifyReturn(method, known);

        return new MethodSpec(
            method.Name,
            $"{method.Name}_{ordinal}",
            method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(Signature),
            method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(TypeOf),
            method.ReturnsByRefReadonly ? "ref readonly " : method.ReturnsByRef ? "ref " : string.Empty,
            kind,
            awaited,
            EquatableArray<ParameterSpec>.From(method.Parameters.Select(p => CreateParameter(p, known))),
            EquatableArray<string>.From(method.TypeParameters.Select(p => p.Name)),
            EquatableArray<string>.From(method.TypeParameters.Select(BuildConstraintClause).Where(c => c is not null)!),
            skipped || blocker is not null);
    }

    /// <summary>
    /// Reports whether a member carries <c>[SkipKonduit]</c>.
    /// </summary>
    /// <remarks>
    /// Checked on the interface method and on the method that implements it, because an interface
    /// from a package you cannot edit leaves the implementation as the only place to put it.
    /// </remarks>
    private static bool HasSkipAttribute(ISymbol? member, KnownTypes known) =>
        member is not null
        && member.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, known.SkipKonduitAttribute));

    private static ParameterSpec CreateParameter(IParameterSymbol parameter, KnownTypes known) =>
        new(
            parameter.Name,
            parameter.Type.ToDisplayString(Signature),
            parameter.Type.ToDisplayString(TypeOf),
            parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty,
            },
            SymbolEqualityComparer.Default.Equals(parameter.Type, known.CancellationToken));

    private static PropertySpec CreateProperty(
        IPropertySymbol property,
        INamedTypeSymbol serviceInterface,
        KnownTypes known,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        var initOnly = property.SetMethod is { IsInitOnly: true };

        if (initOnly)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                KonduitDiagnostics.InitOnlyPropertyCannotBeForwarded,
                property.Locations.FirstOrDefault() ?? location,
                serviceInterface.ToDisplayString() + "." + property.Name));
        }

        return new PropertySpec(
            property.Name,
            property.Type.ToDisplayString(Signature),
            property.GetMethod is not null,
            property.SetMethod switch
            {
                null => null,
                { IsInitOnly: true } => "init",
                _ => "set",
            },
            property.IsIndexer,
            EquatableArray<ParameterSpec>.From(property.Parameters.Select(p => CreateParameter(p, known))));
    }

    private static (ReturnKind Kind, string? Awaited) ClassifyReturn(IMethodSymbol method, KnownTypes known)
    {
        if (method.ReturnsVoid)
        {
            return (ReturnKind.Void, null);
        }

        if (method.ReturnType is not INamedTypeSymbol named)
        {
            return (ReturnKind.Value, null);
        }

        var definition = named.OriginalDefinition;

        if (Matches(definition, known.Task) || Matches(definition, known.ValueTask))
        {
            return (ReturnKind.AwaitableVoid, null);
        }

        if (Matches(definition, known.TaskOfT) || Matches(definition, known.ValueTaskOfT))
        {
            return (ReturnKind.AwaitableValue, named.TypeArguments[0].ToDisplayString(Signature));
        }

        return (ReturnKind.Value, null);
    }

    private static bool Matches(ITypeSymbol candidate, INamedTypeSymbol? known) =>
        known is not null && SymbolEqualityComparer.Default.Equals(candidate, known);

    private static string? DescribeInterceptionBlocker(IMethodSymbol method)
    {
        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
        {
            return "returns by reference";
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind is RefKind.Ref or RefKind.Out)
            {
                return $"has a '{(parameter.RefKind == RefKind.Ref ? "ref" : "out")}' parameter";
            }

            if (CannotBeBoxed(parameter.Type))
            {
                return $"has a parameter of type '{parameter.Type.ToDisplayString()}', which cannot be boxed";
            }
        }

        return CannotBeBoxed(method.ReturnType)
            ? $"returns '{method.ReturnType.ToDisplayString()}', which cannot be boxed"
            : null;
    }

    private static bool CannotBeBoxed(ITypeSymbol type) =>
        type.IsRefLikeType || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer;

    private static string? BuildConstraintClause(ITypeParameterSymbol parameter)
    {
        var constraints = new List<string>();

        if (parameter.HasReferenceTypeConstraint)
        {
            constraints.Add("class");
        }
        else if (parameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (parameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (parameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        constraints.AddRange(parameter.ConstraintTypes.Select(type => type.ToDisplayString(Signature)));

        if (parameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count == 0
            ? null
            : $"where {parameter.Name} : {string.Join(", ", constraints)}";
    }
}
