using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.MediatR;

/// <summary>
/// Adds middleware to the pipeline every wrapped MediatR handler runs through.
/// </summary>
public sealed class KonduitMediatRBuilder
{
    private readonly MediatRRegistration _registration;

    internal KonduitMediatRBuilder(IServiceCollection services, MediatRRegistration registration, int handlerCount)
    {
        Services = services;
        HandlerCount = handlerCount;
        _registration = registration;
    }

    /// <summary>Gets the service collection, so registration can carry on being chained.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets how many handler registrations were wrapped.
    /// </summary>
    /// <remarks>
    /// Handlers carrying <see cref="SkipKonduitAttribute"/> are not counted, because they are left
    /// registered exactly as MediatR registered them.
    /// </remarks>
    public int HandlerCount { get; }

    /// <summary>
    /// Adds a middleware around every wrapped handler.
    /// </summary>
    /// <typeparam name="TMiddleware">
    /// The middleware to add. It is activated from the provider that resolved the handler, with the
    /// next pipeline step supplied as its <see cref="KonduitDelegate"/> constructor parameter.
    /// </typeparam>
    /// <returns>The same builder, so middleware can be chained.</returns>
    /// <remarks>The first middleware added is the outermost, as in ASP.NET Core.</remarks>
    public KonduitMediatRBuilder WithMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IKonduitMiddleware
    {
        _registration.Add(static (provider, next) => ActivatorUtilities.CreateInstance<TMiddleware>(provider, next));
        return this;
    }
}
