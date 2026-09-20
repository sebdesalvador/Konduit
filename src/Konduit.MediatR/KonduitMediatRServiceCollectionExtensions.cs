using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Konduit;
using Konduit.MediatR;
using Konduit.MediatR.Handlers;
using MediatR;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Runs MediatR handlers through a Konduit middleware pipeline.
/// </summary>
public static class KonduitMediatRServiceCollectionExtensions
{
    private static readonly Dictionary<Type, Type> Decorators = new()
    {
        [typeof(IRequestHandler<,>)] = typeof(KonduitRequestHandler<,>),
        [typeof(IRequestHandler<>)] = typeof(KonduitVoidRequestHandler<>),
        [typeof(INotificationHandler<>)] = typeof(KonduitNotificationHandler<,>),
        [typeof(IStreamRequestHandler<,>)] = typeof(KonduitStreamRequestHandler<,>),
    };

    /// <summary>
    /// Wraps every MediatR handler already registered in the collection in a Konduit pipeline.
    /// </summary>
    /// <param name="services">The collection MediatR has registered its handlers into.</param>
    /// <returns>A builder for adding middleware to the pipeline those handlers run through.</returns>
    /// <remarks>
    /// Call this <em>after</em> <c>AddMediatR</c>, because it works by rewriting the registrations
    /// MediatR's assembly scanning produced:
    /// <code>
    /// services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    ///
    /// services.AddKonduitToMediatRHandlers()
    ///         .WithMiddleware&lt;LoggingMiddleware&gt;()
    ///         .WithMiddleware&lt;RetryMiddleware&gt;();
    /// </code>
    /// Request, void request, notification and stream handlers are all wrapped, each keeping its
    /// original lifetime. Konduit middleware runs innermost, inside any MediatR
    /// <c>IPipelineBehavior</c>, so it is closest to the handler itself.
    /// <para>
    /// A handler is left alone when <see cref="SkipKonduitAttribute"/> is present on the handler
    /// class, on its <c>Handle</c> method, or on the request or notification type it handles.
    /// </para>
    /// <para>
    /// Calling this twice is safe: handlers already wrapped are not wrapped again.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No MediatR handlers were found, which almost always means this was called before
    /// <c>AddMediatR</c>.
    /// </exception>
    [RequiresDynamicCode("Konduit.MediatR closes generic decorator types over handler types at run time.")]
    [RequiresUnreferencedCode("Konduit.MediatR inspects handler types reflectively and may not survive trimming.")]
    public static KonduitMediatRBuilder AddKonduitToMediatRHandlers(this IServiceCollection services)
    {
        Throw.IfNull(services);

        var registration = new MediatRRegistration();
        var wrapped = 0;
        var found = 0;

        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];

            if (!IsHandlerRegistration(descriptor, out var handlerDefinition))
            {
                continue;
            }

            found++;

            if (AlreadyWrapped(descriptor) || ShouldSkip(descriptor))
            {
                continue;
            }

            var decoratorType = Decorators[handlerDefinition]
                .MakeGenericType(DecoratorArguments(descriptor, handlerDefinition));

            var activator = new HandlerActivator(decoratorType, CreateInnerFactory(descriptor), registration);

            services[i] = new ServiceDescriptor(descriptor.ServiceType, activator.Create, descriptor.Lifetime);
            wrapped++;
        }

        if (found == 0)
        {
            throw new InvalidOperationException(
                "Konduit found no MediatR handlers to wrap. AddKonduitToMediatRHandlers() rewrites the "
                + "registrations MediatR's assembly scanning produced, so it must be called after AddMediatR().");
        }

        return new KonduitMediatRBuilder(services, registration, wrapped);
    }

    private static bool IsHandlerRegistration(ServiceDescriptor descriptor, out Type handlerDefinition)
    {
        handlerDefinition = null!;

        // Keyed handlers and open generic registrations cannot be closed over a concrete decorator.
        if (descriptor.IsKeyedService
            || !descriptor.ServiceType.IsGenericType
            || descriptor.ServiceType.IsGenericTypeDefinition)
        {
            return false;
        }

        var definition = descriptor.ServiceType.GetGenericTypeDefinition();

        if (!Decorators.ContainsKey(definition))
        {
            return false;
        }

        handlerDefinition = definition;
        return true;
    }

    /// <summary>
    /// Works out the generic arguments to close the decorator over.
    /// </summary>
    /// <remarks>
    /// Notification decorators take the concrete handler type as an extra argument, because MediatR
    /// identifies the subscribers of a notification by their concrete type. Without it, every wrapped
    /// subscriber would share one type and MediatR would run only the first.
    /// </remarks>
    private static Type[] DecoratorArguments(ServiceDescriptor descriptor, Type handlerDefinition)
    {
        var arguments = descriptor.ServiceType.GenericTypeArguments;

        if (handlerDefinition != typeof(INotificationHandler<>))
        {
            return arguments;
        }

        var handlerType = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();

        return handlerType is null
            ? throw new InvalidOperationException(
                $"Konduit cannot wrap the notification handler registered for '{descriptor.ServiceType}' "
                + "because the registration supplies a factory rather than a concrete type. MediatR "
                + "distinguishes the subscribers of a notification by their type, so Konduit needs that type "
                + "to keep them apart. Register the handler by type, or mark it with [SkipKonduit].")
            : [..arguments, handlerType];
    }

    private static bool AlreadyWrapped(ServiceDescriptor descriptor) =>
        descriptor.ImplementationFactory?.Target is HandlerActivator;

    private static bool ShouldSkip(ServiceDescriptor descriptor)
    {
        // The message type carries the attribute for "never instrument this command", wherever it
        // is handled; the handler itself carries it for "do not instrument this one handler".
        if (HasSkipAttribute(descriptor.ServiceType.GenericTypeArguments[0]))
        {
            return true;
        }

        var implementationType = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();

        if (implementationType is null)
        {
            return false;
        }

        return HasSkipAttribute(implementationType)
            || implementationType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(method => method.Name == "Handle" && HasSkipAttribute(method));
    }

    private static bool HasSkipAttribute(MemberInfo member) =>
        member.IsDefined(typeof(SkipKonduitAttribute), inherit: false);

    [RequiresUnreferencedCode("Activating a handler by type may not survive trimming.")]
    private static Func<IServiceProvider, object> CreateInnerFactory(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return _ => instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return factory;
        }

        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException(
                $"Konduit cannot wrap the handler registered for '{descriptor.ServiceType}' because its "
                + "registration has no implementation.");

        return provider => ActivatorUtilities.CreateInstance(provider, implementationType);
    }
}
