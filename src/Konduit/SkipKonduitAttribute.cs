namespace Konduit;

/// <summary>
/// Excludes a single method from its service's Konduit pipeline.
/// </summary>
/// <remarks>
/// A method marked with this attribute is forwarded straight to the target implementation,
/// allocating nothing and running no middleware.
/// <para>
/// It is honoured in two places. On the <b>interface</b> it applies to every implementation of that
/// service. On the <b>implementing method</b> it applies to that implementation alone, which is the
/// option to reach for when the interface comes from a package you cannot edit; the registration
/// has to name the implementation, as in <c>AddScoped&lt;IThirdParty, Ours&gt;()</c>, so the
/// generator can see it.
/// </para>
/// <para>
/// Konduit.MediatR also honours this attribute on a handler class and on the request or notification
/// type itself, in which case that handler is left registered exactly as MediatR registered it.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class, Inherited = false)]
public sealed class SkipKonduitAttribute : Attribute;
