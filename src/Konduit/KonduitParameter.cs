namespace Konduit;

/// <summary>
/// Describes one parameter of an intercepted method.
/// </summary>
/// <param name="Name">The parameter name as declared on the interface.</param>
/// <param name="Type">The declared parameter type.</param>
/// <param name="Position">The zero-based index of this parameter in <see cref="KonduitContext.Arguments"/>.</param>
public sealed record KonduitParameter(string Name, Type Type, int Position);
