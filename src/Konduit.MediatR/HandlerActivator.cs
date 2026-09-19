using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.MediatR;

/// <summary>
/// Creates the Konduit decorator around one handler registration.
/// </summary>
/// <remarks>
/// Instances double as a marker: a service descriptor whose factory targets a
/// <see cref="HandlerActivator"/> has already been wrapped, so calling
/// <c>AddKonduitToMediatRHandlers</c> twice does not nest decorators.
/// </remarks>
internal sealed class HandlerActivator(
    Type decoratorType,
    Func<IServiceProvider, object> innerFactory,
    MediatRRegistration registration)
{
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "The decorator type is closed over the handler's own generic arguments, which "
            + "MediatR already discovers reflectively. The public entry point is annotated.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:DynamicallyAccessedMembers",
        Justification = "The decorator types are referenced directly by this assembly and so are rooted.")]
    public object Create(IServiceProvider services) =>
        Activator.CreateInstance(
            decoratorType,
            innerFactory(services),
            registration.BuildPipeline(services),
            services)!;
}
