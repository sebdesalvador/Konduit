namespace Konduit.SourceGeneration.Model;

/// <summary>One method a generated proxy has to implement.</summary>
/// <param name="Name">The method name.</param>
/// <param name="MemberId">A unique identifier for this method's generated statics, disambiguating overloads.</param>
/// <param name="ReturnTypeFullyQualified">The declared return type, or <c>void</c>.</param>
/// <param name="ReturnTypeForTypeOf">The declared return type, valid inside <c>typeof</c>.</param>
/// <param name="ReturnModifier">The <c>ref</c> or <c>ref readonly</c> return modifier, or an empty string.</param>
/// <param name="Kind">How the return value is produced.</param>
/// <param name="AwaitedTypeFullyQualified">The awaited value type for <see cref="ReturnKind.AwaitableValue"/>.</param>
/// <param name="Parameters">The declared parameters, in order.</param>
/// <param name="TypeParameters">The method's own type parameters, if it is generic.</param>
/// <param name="ConstraintClauses">Constraint clauses to repeat on the emitted method.</param>
/// <param name="PassThrough">Whether the method forwards straight to the target, skipping the pipeline.</param>
internal sealed record MethodSpec(
    string Name,
    string MemberId,
    string ReturnTypeFullyQualified,
    string ReturnTypeForTypeOf,
    string ReturnModifier,
    ReturnKind Kind,
    string? AwaitedTypeFullyQualified,
    EquatableArray<ParameterSpec> Parameters,
    EquatableArray<string> TypeParameters,
    EquatableArray<string> ConstraintClauses,
    bool PassThrough)
{
    public bool IsGeneric => TypeParameters.Count > 0;

    public bool IsAsync => !PassThrough && Kind is ReturnKind.AwaitableVoid or ReturnKind.AwaitableValue;
}
