using System.Runtime.CompilerServices;

namespace Konduit;

/// <summary>Argument guards that work on every target framework.</summary>
/// <remarks>
/// <c>ArgumentNullException.ThrowIfNull</c> arrived in .NET 6, so it cannot be used on the
/// netstandard2.0 target. This wrapper keeps call sites uniform and delegates to it where it exists.
/// </remarks>
internal static class Throw
{
    public static void IfNull(
        object? argument,
        [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
    {
#if NETSTANDARD2_0
        if (argument is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#else
        ArgumentNullException.ThrowIfNull(argument, parameterName);
#endif
    }
}
