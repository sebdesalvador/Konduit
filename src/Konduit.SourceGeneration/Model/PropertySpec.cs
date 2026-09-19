namespace Konduit.SourceGeneration.Model;

/// <summary>A property or indexer, forwarded to the target without interception.</summary>
/// <param name="Name">The property name, unused for an indexer.</param>
/// <param name="TypeFullyQualified">The globally qualified property type.</param>
/// <param name="HasGetter">Whether the interface declares a getter.</param>
/// <param name="SetterKeyword">The setter keyword to emit — <c>set</c>, <c>init</c>, or null when there is none.</param>
/// <param name="IsIndexer">Whether this is an indexer rather than a named property.</param>
/// <param name="Parameters">The indexer parameters, empty for a named property.</param>
internal sealed record PropertySpec(
    string Name,
    string TypeFullyQualified,
    bool HasGetter,
    string? SetterKeyword,
    bool IsIndexer,
    EquatableArray<ParameterSpec> Parameters);
