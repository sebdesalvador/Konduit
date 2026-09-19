using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Konduit.SourceGeneration.Model;
using Microsoft.CodeAnalysis;

namespace Konduit.SourceGeneration.Parsing;

/// <summary>Turns a service interface symbol into the model the emitter writes from.</summary>
internal static partial class ProxySpecFactory
{
    /// <summary>Renders types for use in signatures, keeping nullable reference annotations.</summary>
    public static readonly SymbolDisplayFormat Signature = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Renders types for use inside <c>typeof</c>, where <c>string?</c> would not compile.</summary>
    public static readonly SymbolDisplayFormat TypeOf = SymbolDisplayFormat.FullyQualifiedFormat;

    public static ProxySpec Create(
        INamedTypeSymbol serviceInterface,
        KnownTypes known,
        Location location,
        List<DiagnosticInfo> diagnostics,
        CancellationToken cancellationToken)
    {
        var methods = new List<MethodSpec>();
        var properties = new List<PropertySpec>();
        var events = new List<EventSpec>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in EnumerateMembers(serviceInterface))
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (member)
            {
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method when seen.Add(SignatureKey(method)):
                    methods.Add(CreateMethod(method, methods.Count, serviceInterface, known, location, diagnostics));
                    break;

                case IPropertySymbol property when seen.Add(SignatureKey(property)):
                    properties.Add(CreateProperty(property, serviceInterface, known, location, diagnostics));
                    break;

                case IEventSymbol @event when seen.Add("event " + @event.Name):
                    events.Add(new EventSpec(@event.Name, @event.Type.ToDisplayString(Signature)));
                    break;
            }
        }

        return new ProxySpec(
            serviceInterface.ToDisplayString(Signature),
            serviceInterface.ToDisplayString(TypeOf),
            BuildProxyTypeName(serviceInterface),
            EquatableArray<MethodSpec>.From(methods),
            EquatableArray<PropertySpec>.From(properties),
            EquatableArray<EventSpec>.From(events));
    }

    private static IEnumerable<ISymbol> EnumerateMembers(INamedTypeSymbol serviceInterface) =>
        new[] { serviceInterface }
            .Concat(serviceInterface.AllInterfaces)
            .SelectMany(type => type.GetMembers())
            // Static members need no forwarding, and a default implementation already dispatches
            // correctly through the interface without the proxy redeclaring it.
            .Where(member => !member.IsStatic && member.IsAbstract);

    private static string SignatureKey(IMethodSymbol method) =>
        $"{method.Name}`{method.Arity}({string.Join(",", method.Parameters.Select(p => p.RefKind + p.Type.ToDisplayString(TypeOf)))})";

    private static string SignatureKey(IPropertySymbol property) =>
        $"{property.Name}[{string.Join(",", property.Parameters.Select(p => p.Type.ToDisplayString(TypeOf)))}]";

    private static string BuildProxyTypeName(INamedTypeSymbol serviceInterface)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in serviceInterface.ToDisplayString(TypeOf).Replace("global::", string.Empty))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().TrimEnd('_') + "KonduitProxy";
    }
}
