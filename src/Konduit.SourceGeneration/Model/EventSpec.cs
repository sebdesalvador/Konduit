namespace Konduit.SourceGeneration.Model;

/// <summary>An event, forwarded to the target without interception.</summary>
/// <param name="Name">The event name.</param>
/// <param name="TypeFullyQualified">The globally qualified handler type.</param>
internal sealed record EventSpec(string Name, string TypeFullyQualified);
