namespace Konduit.SourceGeneration.Model;

/// <summary>One parameter of a proxied method.</summary>
/// <param name="Name">The parameter name, reused verbatim in the emitted signature.</param>
/// <param name="TypeFullyQualified">The globally qualified parameter type, with nullable annotations.</param>
/// <param name="TypeForTypeOf">The same type without reference-type nullable annotations, valid inside <c>typeof</c>.</param>
/// <param name="Modifier">The declared <c>ref</c>/<c>out</c>/<c>in</c> keyword with a trailing space, or an empty string.</param>
/// <param name="IsCancellationToken">Whether this parameter supplies the context's cancellation token.</param>
internal sealed record ParameterSpec(
    string Name,
    string TypeFullyQualified,
    string TypeForTypeOf,
    string Modifier,
    bool IsCancellationToken);
