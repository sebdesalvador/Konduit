namespace Konduit;

/// <summary>
/// Excludes a single interface method from its service's Konduit pipeline.
/// </summary>
/// <remarks>
/// The generated proxy forwards calls to a method marked with this attribute straight to the target
/// implementation, allocating nothing and running no middleware. Apply it on the interface
/// declaration; an attribute on the implementing method has no effect, because callers reach the
/// proxy through the interface.
/// <para>
/// Konduit.MediatR also honours this attribute on a handler class, on its <c>Handle</c> method, and
/// on the request or notification type itself, in which case that handler is left registered exactly
/// as MediatR registered it.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class, Inherited = false)]
public sealed class SkipKonduitAttribute : Attribute;
