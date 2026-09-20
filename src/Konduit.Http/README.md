# Konduit.Http

Adds [Konduit](https://github.com/sebdesalvador/Konduit) middleware to typed `HttpClient` services.

```csharp
services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://api.example.com"))
        .AddMiddleware<LoggingMiddleware>()
        .AddMiddleware<RetryMiddleware>();
```

`AddHttpClient<TClient, TImplementation>()` returns an `IHttpClientBuilder`, which has no registration
call to chain `WithMiddleware<T>()` onto. `AddMiddleware<T>()` fills that gap.

## This is not a DelegatingHandler

The distinction matters, and it's the reason this package exists.

| | Sees | Use it for |
|---|---|---|
| `DelegatingHandler` | `HttpRequestMessage` / `HttpResponseMessage` | retries on status codes, auth headers, HTTP-level tracing |
| `AddMiddleware<T>()` | the **interface method** and its return value | the same cross-cutting concerns you apply to ordinary services |

Middleware here sees `GetOrderAsync(7)` returning an `Order` — not a `GET /orders/7` returning
`200 OK`. So the same `LoggingMiddleware` or `RetryMiddleware` you wrote for a repository works
unchanged on an API client, and a short-circuiting cache middleware can skip the call entirely
without the HTTP stack being involved.

Both compose: a handler still runs inside the client, underneath the Konduit pipeline.

## Writing middleware

Exactly as for any Konduit service — nothing HTTP-specific:

```csharp
public sealed class RetryMiddleware(KonduitDelegate next, ILogger<RetryMiddleware> logger)
    : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await next(context);
                return;
            }
            catch (HttpRequestException ex) when (attempt < 3)
            {
                logger.LogWarning(ex, "{Method} failed, retrying", context.Method.Name);
            }
        }
    }
}
```

The client keeps its transient lifetime, and the first middleware added is the outermost.

## Opting out

`[SkipKonduit]` on an interface method forwards it straight to the client:

```csharp
public interface IOrderApi
{
    Task<Order> GetAsync(int id, CancellationToken cancellationToken);

    [SkipKonduit]
    Task<string> PingAsync(CancellationToken cancellationToken);
}
```

## When the chain is broken

The source generator reads the registration chain **syntactically** to learn the client type, so it
has to be able to see it. This works:

```csharp
services.AddHttpClient<IOrderApi, OrderApi>()
        .ConfigurePrimaryHttpMessageHandler(() => handler)   // intermediate calls are fine
        .AddMiddleware<LoggingMiddleware>();
```

This does not, because the builder is in a variable and the chain is no longer visible:

```csharp
var builder = services.AddHttpClient<IOrderApi, OrderApi>();
builder.AddMiddleware<LoggingMiddleware>();                   // build error KDT001
```

Name the service explicitly and it works anywhere:

```csharp
builder.AddMiddleware<IOrderApi, LoggingMiddleware>();
```

This requirement is also a safety net. The runtime resolves the client by scanning backwards, which
is only unambiguous while `AddMiddleware` sits in the chain of its own registration — and that is
exactly when the generator can see it. When it can't, you get a build error rather than a client
silently wrapped in the wrong pipeline.

## Named clients

"Named client" covers two different registrations, and they behave differently.

**A typed client with a name** works normally, including the single-parameter form:

```csharp
services.AddHttpClient<IOrderApi, OrderApi>("orders", c => c.BaseAddress = new Uri("..."))
        .AddMiddleware<LoggingMiddleware>();
```

`builder.Name` is `"orders"` rather than `"IOrderApi"`, so the name can't identify the client — but
the chain can, and the rule above guarantees this is unambiguous. Configuring several named clients
one after another is fine; each resolves to its own.

**A bare named client cannot be wrapped**, because there is no service to wrap:

```csharp
services.AddHttpClient("orders").AddMiddleware<LoggingMiddleware>();   // build error KDT004
```

`AddHttpClient("orders")` registers no interface. The client is reached through
`IHttpClientFactory.CreateClient("orders")`, so there are no interface method calls to intercept —
Konduit works through interfaces, and here there isn't one. This fails at build time, not silently.

The fix is to put the middleware on the service that *uses* the named client:

```csharp
services.AddHttpClient("orders", c => c.BaseAddress = new Uri("..."));

services.AddScoped<IOrderApi>(sp =>
            new OrderApi(sp.GetRequiredService<IHttpClientFactory>().CreateClient("orders")))
        .WithMiddleware<LoggingMiddleware>();
```

That is core Konduit's `WithMiddleware<T>()` on a factory registration — the named client keeps its
own HTTP configuration, and the service in front of it gets the pipeline.

## How the client is found

`AddHttpClient<TClient, TImplementation>()` adds more than twenty service descriptors, and every
builder call adds more, so the client's own descriptor is not reliably the last one. It's found by
scanning backwards for a registration that could only be a Konduit-wrapped typed client: an
interface, registered by factory, for which the generator produced a proxy — and the generator only
produces one for interfaces named in a Konduit chain. The builder's own name is preferred where it
matches, so configuring several clients before adding middleware still resolves each correctly.

If it can't identify the registration, it throws at startup with an explanation rather than quietly
leaving the client unwrapped.

## Requirements

Targets `netstandard2.0`, `net8.0` and `net10.0`, alongside `Microsoft.Extensions.Http`. Building
needs the .NET 6 SDK or later, since the proxies come from an incremental source generator.

The Konduit source generator flows in with this package; no separate reference is needed.
