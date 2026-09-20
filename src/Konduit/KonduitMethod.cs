using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Konduit;

/// <summary>
/// Describes an intercepted interface method.
/// </summary>
/// <remarks>
/// Instances are emitted once per method by the source generator and shared across every call, so
/// reading <see cref="Name"/>, <see cref="Parameters"/> or <see cref="ReturnType"/> from middleware
/// costs nothing. <see cref="MethodInfo"/> is the one member that pays for reflection, and only on
/// first access.
/// </remarks>
public sealed class KonduitMethod
{
    private const DynamicallyAccessedMemberTypes DeclaredMethods =
        DynamicallyAccessedMemberTypes.PublicMethods;

    [DynamicallyAccessedMembers(DeclaredMethods)]
    private readonly Type _declaringType;

    private MethodInfo? _methodInfo;

    /// <summary>
    /// Initializes a new <see cref="KonduitMethod"/>.
    /// </summary>
    /// <param name="declaringType">The interface that declares the method.</param>
    /// <param name="name">The method name.</param>
    /// <param name="returnType">The declared return type, before any task unwrapping.</param>
    /// <param name="parameters">The declared parameters, in declaration order.</param>
    public KonduitMethod(
        [DynamicallyAccessedMembers(DeclaredMethods)] Type declaringType,
        string name,
        Type returnType,
        IReadOnlyList<KonduitParameter> parameters)
    {
        Throw.IfNull(declaringType);
        Throw.IfNull(name);
        Throw.IfNull(returnType);
        Throw.IfNull(parameters);

        _declaringType = declaringType;
        Name = name;
        ReturnType = returnType;
        Parameters = parameters;
    }

    /// <summary>Gets the method name.</summary>
    public string Name { get; }

    /// <summary>Gets the interface that declares this method.</summary>
    public Type DeclaringType => _declaringType;

    /// <summary>
    /// Gets the declared return type, before any task unwrapping.
    /// </summary>
    /// <remarks>
    /// For an <c>async Task&lt;Order&gt;</c> method this is <c>Task&lt;Order&gt;</c>, while
    /// <see cref="KonduitContext.Result"/> holds the awaited <c>Order</c>.
    /// </remarks>
    public Type ReturnType { get; }

    /// <summary>Gets the declared parameters, in declaration order.</summary>
    public IReadOnlyList<KonduitParameter> Parameters { get; }

    /// <summary>
    /// Gets the reflected method, resolved and cached on first access.
    /// </summary>
    /// <exception cref="InvalidOperationException">The method could not be found on the interface.</exception>
    public MethodInfo MethodInfo => _methodInfo ??= Resolve();

    private MethodInfo Resolve()
    {
        var parameterTypes = new Type[Parameters.Count];
        for (var i = 0; i < parameterTypes.Length; i++)
        {
            parameterTypes[i] = Parameters[i].Type;
        }

        return _declaringType.GetMethod(Name, parameterTypes)
            ?? throw new InvalidOperationException(
                $"Konduit could not resolve the method '{Name}' on '{_declaringType}'. " +
                "This usually means the interface changed after the proxy was generated; rebuild the project.");
    }
}
