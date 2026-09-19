# Konduit.MediatR

Runs MediatR handlers through a [Konduit](https://github.com/sebdesalvador/Konduit) middleware pipeline.

MediatR discovers handlers by scanning assemblies, so there's no registration call to chain
`WithMiddleware<T>()` onto. This package adds one call that wraps everything MediatR found:

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

services.AddKonduitToMediatRHandlers()
        .WithMiddleware<LoggingMiddleware>()
        .WithMiddleware<RetryMiddleware>();
```

Every handler now runs inside that pipeline, using the same middleware you write for ordinary
services — no separate MediatR-specific abstraction.

```csharp
public sealed class LoggingMiddleware(KonduitDelegate next, ILogger<LoggingMiddleware> logger)
    : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        logger.LogInformation("→ {Handler}", context.Target.GetType().Name);
        await next(context);
        logger.LogInformation("← {Handler} = {Result}", context.Target.GetType().Name, context.Result);
    }
}
```

On the context: `Arguments[0]` is the request or notification, `Arguments[1]` the cancellation token,
`Target` the real handler, and `Result` the awaited response.

## Order matters

Call it **after** `AddMediatR`. It works by rewriting the registrations MediatR's scanning produced,
so there is nothing to rewrite if it runs first — and it throws with that explanation rather than
silently doing nothing.

## What gets wrapped

| Handler | Wrapped | `Result` |
|---|---|---|
| `IRequestHandler<TRequest, TResponse>` | ✅ | the awaited response |
| `IRequestHandler<TRequest>` | ✅ | `null` |
| `INotificationHandler<TNotification>` | ✅ one pipeline per subscriber | `null` |
| `IStreamRequestHandler<TRequest, TResponse>` | ✅ | the `IAsyncEnumerable<T>` |

Each keeps its original lifetime, and handlers stay resolvable through their usual interfaces.

## Opting out

`[SkipKonduit]` leaves a handler registered exactly as MediatR registered it — no wrapper, no
overhead. It's honoured in three places:

```csharp
[SkipKonduit]                                   // this handler
public sealed class ExportHandler : IRequestHandler<Export, Stream> { ... }

public sealed class ReportHandler : IRequestHandler<Report, string>
{
    [SkipKonduit]                               // this Handle method
    public Task<string> Handle(Report request, CancellationToken cancellationToken) => ...;
}

[SkipKonduit]                                   // this message, wherever it is handled
public sealed record Heartbeat : INotification;
```

## Where Konduit sits relative to MediatR behaviours

Konduit wraps the handler itself, so its middleware runs **innermost** — inside every
`IPipelineBehavior`:

```
MediatR behaviour → … → Konduit middleware → … → your handler
```

Konduit doesn't replace MediatR behaviours; use behaviours for things that belong to MediatR's model
(and that need to see the request before a handler is resolved), and Konduit for cross-cutting
concerns you want applied uniformly across handlers *and* ordinary services.

## Notification handlers and type identity

MediatR identifies the subscribers of a notification by their **concrete type**. A naive decorator
that wrapped every subscriber in one shared type would collapse them, and MediatR would silently run
only the first — handlers would stop firing with no error.

Konduit.MediatR avoids this by closing each notification decorator over the handler type, so
`EmailOnPlaced` and `AuditOnPlaced` stay distinct and both still run. There's a test pinning it.

The consequence: a notification handler registered by **factory** rather than by type can't be
wrapped, because its type isn't knowable at registration. That throws with an explanation rather
than quietly dropping a subscriber. Register it by type, or mark it `[SkipKonduit]`.

## Caveats

**Streams measure the call, not the iteration.** MediatR defers a stream handler until enumeration
begins; when it does, the pipeline runs once and unwinds as soon as the handler returns its
`IAsyncEnumerable<T>`, before any item is produced. Same rule Konduit applies to `IAsyncEnumerable<T>`
on ordinary services.

**Calling it twice is safe.** Already-wrapped handlers are not wrapped again, and `HandlerCount`
reports how many were wrapped on that call.

**Keyed and open generic handler registrations are skipped**, since neither can be closed over a
concrete decorator type.

**Not trim- or AOT-compatible.** Decorator types are closed over handler types at run time, which is
inherent to MediatR's own reflective model. The entry point is annotated with `[RequiresDynamicCode]`
and `[RequiresUnreferencedCode]`.

## Licensing note

This package is MIT, but it depends on **MediatR**, which since v13 is dual-licensed under
**RPL-1.5** (a strong copyleft that requires releasing your source) or a paid commercial licence.
Adding this package pulls MediatR in. If you already use MediatR that changes nothing for you — but
it's worth knowing which obligations apply to your own code.

## Requirements

.NET 8 or later, MediatR 14 or later. MediatR 14 also requires `services.AddLogging()` before
`AddMediatR`.
