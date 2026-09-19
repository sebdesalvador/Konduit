namespace Konduit.SourceGeneration.Model;

/// <summary>Everything needed to emit one proxy class.</summary>
/// <param name="InterfaceFullyQualified">The globally qualified service interface.</param>
/// <param name="InterfaceForTypeOf">The same interface, written so it is valid inside <c>typeof</c>.</param>
/// <param name="ProxyTypeName">The name of the generated class.</param>
/// <param name="Methods">The interface's methods, including inherited ones.</param>
/// <param name="Properties">The interface's properties and indexers.</param>
/// <param name="Events">The interface's events.</param>
internal sealed record ProxySpec(
    string InterfaceFullyQualified,
    string InterfaceForTypeOf,
    string ProxyTypeName,
    EquatableArray<MethodSpec> Methods,
    EquatableArray<PropertySpec> Properties,
    EquatableArray<EventSpec> Events);
