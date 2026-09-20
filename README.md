# Konduit

[![CI](https://github.com/sebdesalvador/Konduit/actions/workflows/ci.yml/badge.svg)](https://github.com/sebdesalvador/Konduit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Konduit.svg)](https://www.nuget.org/packages/Konduit)

ASP.NET Core style middleware pipelines for any dependency-injected service.

Register a service the way you already do, and chain `WithMiddleware<T>()` to wrap every call to it:

```csharp
services.AddScoped<IOrderService, OrderService>()
        .WithMiddleware<LoggingMiddleware>()
        .WithMiddleware<RetryMiddleware>();
```

Resolving `IOrderService` now gives you a proxy. Every call travels `Logging → Retry → OrderService`,
and the "after" half of each middleware unwinds on the way back out — exactly like `app.Use(...)`,
but for method calls instead of HTTP requests.

Proxies are **source-generated**, so there is no reflection on the call path and nothing to configure.

## Writing middleware

Middleware takes the next step in the pipeline as a `KonduitDelegate` constructor parameter, just as
ASP.NET Core middleware takes a `RequestDelegate`. Every other constructor parameter comes from DI.

```csharp
public sealed class LoggingMiddleware(KonduitDelegate next, ILogger<LoggingMiddleware> logger)
    : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        logger.LogInformation("→ {Method}", context.Method.Name);

        var started = Stopwatch.GetTimestamp();
        await next(context);

        logger.LogInformation("← {Method} in {Elapsed}",
            context.Method.Name, Stopwatch.GetElapsedTime(started));
    }
}
```

The pipeline is built **per service resolution, from the resolving scope**, so middleware can inject
scoped dependencies directly:

```csharp
public sealed class AuditMiddleware(KonduitDelegate next, AppDbContext db) : IKonduitMiddleware
```

### What's on the context

| Member | |
|---|---|
| `Method` | Name, declaring type, return type, parameters. `Method.MethodInfo` is resolved lazily, only if you ask. |
| `Arguments` | The call arguments. Writing to the array before `next` changes what the target receives. |
| `Result` | The return value. For async methods this is the **awaited** value, not the task. |
| `Target` | The implementation being wrapped. |
| `Services` | The provider that resolved the service. |
| `CancellationToken` | Lifted from the method's own token parameter when it has one. |
| `Items` | A bag for passing state between middleware. Allocated on first use. |

Because `Result` holds the awaited value, a middleware wrapped around `Task<Order> PlaceAsync()`
sees the finished `Order` after `await next(context)` — not a `Task` that hasn't run yet.

### Things middleware can do

```csharp
// Rewrite an argument on the way in
context.Arguments[0] = Sanitize((string)context.Arguments[0]!);

// Rewrite the result on the way out
await next(context);
context.Result = Redact((Order)context.Result!);

// Short-circuit: skip the target entirely
if (cache.TryGetValue(key, out var hit))
{
    context.Result = hit;
    return;               // never calls next
}

// Handle failures
try { await next(context); }
catch (TimeoutException) { context.Result = Order.Unavailable; }
```

## What gets intercepted

Every method declared on the service interface, including inherited ones.

Opt a single method out with `[SkipKonduit]` — it is forwarded straight to the implementation,
allocating nothing and running no middleware:

```csharp
public interface IOrderService
{
    Task<Order> PlaceAsync(Cart cart, CancellationToken cancellationToken);

    [SkipKonduit]
    string Describe(int id);      // hot path, no middleware
}
```

**Properties, indexers and events are forwarded without interception.** So are methods Konduit
cannot proxy — each one produces a build warning naming the method and the reason, so nothing is
silently uninstrumented.

Interception happens **through the interface**. A call from inside `OrderService` to its own method
does not pass through the pipeline, and neither does a call through a concrete `OrderService`
reference. The implementation stays resolvable unwrapped if you want it:

```csharp
provider.GetRequiredService<IOrderService>();   // the proxy
provider.GetRequiredService<OrderService>();    // the real thing, no pipeline
```

## Registration

`WithMiddleware<T>()` decorates the service registered immediately before it, so **chain it directly
onto the registration**:

```csharp
// Good
services.AddScoped<IOrderService, OrderService>().WithMiddleware<LoggingMiddleware>();

// Build error KDT001 — Konduit can't tell which service this belongs to
services.AddScoped<IOrderService, OrderService>();
services.WithMiddleware<LoggingMiddleware>();
```

`AddScoped`, `AddSingleton` and `AddTransient` all work, in their generic, factory, instance and
`typeof(...)` forms. The registration keeps its original lifetime.

Konduit does **not** replace the built-in registration methods. An `AddScoped<TService, TImpl>()` of
its own would have the same signature as the BCL's and make every registration in your project
ambiguous, so `WithMiddleware<T>()` is the only extension added.

### Not supported

Keyed services and open generics both throw at registration time with a message naming the type.
Register each closed generic separately if you need middleware on it:

```csharp
services.AddScoped<IRepository<Order>, OrderRepository>().WithMiddleware<LoggingMiddleware>();
```

## Build diagnostics

| ID | | |
|---|---|---|
| `KDT001` | error | `WithMiddleware<T>()` is not chained onto a service registration |
| `KDT002` | error | The service is not registered as an interface |
| `KDT003` | warning | A method cannot be intercepted and will bypass the pipeline |
| `KDT004` | error | The registration shape was not recognised |
| `KDT006` | warning | An init-only interface property cannot be forwarded |

## Caveats

**Synchronous methods and async middleware.** Sync methods are supported: the proxy runs the same
pipeline and, when every middleware completed synchronously — the usual case for logging, validation
or metrics — takes the result with no blocking at all. It blocks only if a middleware genuinely went
async inside a synchronous call, which carries the usual sync-over-async deadlock risk. If your
middleware does real I/O, prefer async methods on the interface.

**`IAsyncEnumerable<T>` is not streamed.** A method returning `IAsyncEnumerable<T>` is treated as an
ordinary value-returning method: middleware sees the enumerable object as `Result`, and the "after"
half runs when the method returns, *before* anything is enumerated. Timing or logging middleware on
such a method measures the call, not the iteration.

**Init-only interface properties cannot be forwarded.** No proxy can assign another object's
init-only member. The generated accessor throws `NotSupportedException` and `KDT006` warns at build
time.

**Default interface members are not intercepted directly.** They still dispatch correctly, and calls
they make back onto the interface *do* go through the pipeline.

## Performance

Proxies are generated at compile time, so an intercepted call costs a direct virtual call plus the
context and argument-array allocations — no reflection, no IL emit, no dynamic dispatch. Method
descriptors and the terminal delegates are static and shared across calls. `[SkipKonduit]` methods
compile down to a direct forward.

The library is trim- and AOT-compatible.

## MediatR

MediatR discovers handlers by scanning assemblies, so there is no registration call to chain
`WithMiddleware<T>()` onto. The companion package **[Konduit.MediatR](src/Konduit.MediatR/README.md)**
adds one call that wraps everything MediatR found:

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

services.AddKonduitToMediatRHandlers()
        .WithMiddleware<LoggingMiddleware>()
        .WithMiddleware<RetryMiddleware>();
```

Commands, queries, notifications and streams all run through the same middleware you write for
ordinary services, with `[SkipKonduit]` opting a handler out.

## Typed HttpClient services

`AddHttpClient<TClient, TImplementation>()` returns an `IHttpClientBuilder`, with no registration
call to chain `WithMiddleware<T>()` onto. The companion package
**[Konduit.Http](src/Konduit.Http/README.md)** adds `AddMiddleware<T>()`:

```csharp
services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://api.example.com"))
        .AddMiddleware<LoggingMiddleware>()
        .AddMiddleware<RetryMiddleware>();
```

This wraps calls to *the service*, not the underlying `HttpClient`: middleware sees
`GetAsync(7)` returning an `Order`, not a `GET /orders/7` returning `200 OK`. Reach for a
`DelegatingHandler` when you want the HTTP exchange itself — the two compose.

## Requirements

.NET 8 or later. The source generator ships inside the `Konduit` package; no separate reference is
needed.
