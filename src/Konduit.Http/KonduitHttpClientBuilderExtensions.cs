using System.Diagnostics.CodeAnalysis;
using Konduit;
using Konduit.Http;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds Konduit middleware to typed <see cref="System.Net.Http.HttpClient"/> services.
/// </summary>
public static class KonduitHttpClientBuilderExtensions
{
    /// <summary>
    /// Wraps the typed client this builder configures in a Konduit pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">
    /// The middleware to add. It is activated from the provider that resolved the client, with the
    /// next pipeline step supplied as its <see cref="KonduitDelegate"/> constructor parameter.
    /// </typeparam>
    /// <param name="builder">The builder returned by <c>AddHttpClient&lt;TClient, TImplementation&gt;()</c>.</param>
    /// <returns>The same builder, so client configuration and further middleware can be chained.</returns>
    /// <remarks>
    /// This wraps calls to <em>the service</em>, not the underlying <see cref="System.Net.Http.HttpClient"/>:
    /// middleware sees the interface method that was called and the value it returned, not an
    /// <c>HttpRequestMessage</c>. Use a <see cref="System.Net.Http.DelegatingHandler"/> when you want
    /// to act on the HTTP exchange itself; use this when you want the same middleware you apply to
    /// ordinary services.
    /// <code>
    /// services.AddHttpClient&lt;IOrderApi, OrderApi&gt;(c => c.BaseAddress = new Uri("https://api.example.com"))
    ///         .AddMiddleware&lt;LoggingMiddleware&gt;()
    ///         .AddMiddleware&lt;RetryMiddleware&gt;();
    /// </code>
    /// The client keeps its transient lifetime, and the first middleware added is the outermost.
    /// <para>
    /// The client type is inferred from the registration. Use
    /// <see cref="AddMiddleware{TClient, TMiddleware}(IHttpClientBuilder)"/> to name it explicitly.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The typed client registration could not be identified.</exception>
    public static IHttpClientBuilder AddMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(
        this IHttpClientBuilder builder)
        where TMiddleware : class, IKonduitMiddleware
    {
        Throw.IfNull(builder);

        Resolve(builder, clientType: null)
            .Add(static (provider, next) => ActivatorUtilities.CreateInstance<TMiddleware>(provider, next));

        return builder;
    }

    /// <summary>
    /// Wraps a named typed client in a Konduit pipeline.
    /// </summary>
    /// <typeparam name="TClient">The client interface to wrap.</typeparam>
    /// <typeparam name="TMiddleware">The middleware to add.</typeparam>
    /// <param name="builder">The builder returned by <c>AddHttpClient&lt;TClient, TImplementation&gt;()</c>.</param>
    /// <returns>The same builder, so client configuration and further middleware can be chained.</returns>
    /// <remarks>
    /// Naming the client removes all guesswork about which registration is being wrapped. Reach for
    /// this when several typed clients are configured together, or when the single-parameter overload
    /// reports that it cannot identify the registration.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TClient"/> is not a registered typed client.</exception>
    public static IHttpClientBuilder AddMiddleware<
        TClient,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(
        this IHttpClientBuilder builder)
        where TClient : class
        where TMiddleware : class, IKonduitMiddleware
    {
        Throw.IfNull(builder);

        Resolve(builder, typeof(TClient))
            .Add(static (provider, next) => ActivatorUtilities.CreateInstance<TMiddleware>(provider, next));

        return builder;
    }

    private static HttpClientRegistration Resolve(IHttpClientBuilder builder, Type? clientType)
    {
        var services = builder.Services;
        var index = FindTypedClient(services, builder.Name, clientType);

        if (index < 0)
        {
            throw new InvalidOperationException(BuildNotFoundMessage(builder.Name, clientType));
        }

        var descriptor = services[index];

        // Already wrapped by an earlier AddMiddleware on the same client: add to that pipeline
        // rather than nesting a second proxy around the first.
        if (descriptor.ImplementationFactory?.Target is HttpClientRegistration existing)
        {
            return existing;
        }

        var registration = new HttpClientRegistration(descriptor.ServiceType, descriptor.ImplementationFactory!);

        services[index] = new ServiceDescriptor(descriptor.ServiceType, registration.CreateProxy, descriptor.Lifetime);

        return registration;
    }

    /// <summary>
    /// Finds the descriptor <c>AddHttpClient&lt;TClient, TImplementation&gt;()</c> created for its client.
    /// </summary>
    /// <remarks>
    /// That call adds more than twenty descriptors and anything else chained onto the builder adds
    /// more still, so the client's own descriptor is not reliably the last one. It is found instead
    /// by scanning backwards for a registration that could only be a Konduit-wrapped typed client:
    /// an interface, registered by factory, for which the source generator produced a proxy. The
    /// generator only produces one for interfaces named in a Konduit chain, which is what makes the
    /// match precise. The client's own name is preferred where it matches, so configuring several
    /// clients before adding middleware still resolves each to the right one.
    /// </remarks>
    private static int FindTypedClient(IServiceCollection services, string builderName, Type? clientType)
    {
        if (clientType is not null)
        {
            return LastIndexOf(services, descriptor => descriptor.ServiceType == clientType && IsCandidate(descriptor));
        }

        var named = LastIndexOf(
            services,
            descriptor => IsCandidate(descriptor) && descriptor.ServiceType.Name == builderName);

        return named >= 0 ? named : LastIndexOf(services, IsCandidate);
    }

    private static int LastIndexOf(IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (predicate(services[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsCandidate(ServiceDescriptor descriptor) =>
        !descriptor.IsKeyedService
        && descriptor.ServiceType.IsInterface
        && descriptor.ImplementationFactory is not null
        && (descriptor.ImplementationFactory.Target is HttpClientRegistration
            || KonduitProxyRegistry.IsRegistered(descriptor.ServiceType));

    private static string BuildNotFoundMessage(string builderName, Type? clientType)
    {
        var subject = clientType is null
            ? $"the typed client for '{builderName}'"
            : $"the typed client '{clientType}'";

        return $"Konduit could not find {subject} to wrap. AddMiddleware<T>() must be chained onto "
            + "AddHttpClient<TClient, TImplementation>() where TClient is an interface, since calls reach a "
            + "Konduit proxy through the interface. If the registration is an interface, check the build output "
            + "for Konduit diagnostics: the source generator emits a proxy for each service named in a chain "
            + "ending in AddMiddleware<T>(), and without one there is nothing to wrap.";
    }
}
