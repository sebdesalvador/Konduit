# Samples

Three runnable console apps, one per package. Each prints what the pipeline did, so you can see the
ordering rather than infer it. They build as part of the solution, so they cannot drift from the
code.

```bash
dotnet run --project samples/Konduit.Sample
dotnet run --project samples/Konduit.MediatR.Sample
dotnet run --project samples/Konduit.Http.Sample
```

## Konduit.Sample

Ordinary services: `AddSingleton<IOrderService, OrderService>().WithMiddleware<T>()`.

Shows logging, a retry that calls `next` more than once, a cache that short-circuits the target
entirely, and a `[SkipKonduit]` method that runs no middleware.

## Konduit.MediatR.Sample

Handlers found by assembly scanning, wrapped with one `AddKonduitToMediatRHandlers()` call.

Covers a query where middleware sees the awaited response, a command returning nothing, and an event
with **two** subscribers — the pipeline runs once around each, which is the behaviour that breaks if
the subscribers ever lose their distinct types. Also shows validation rejecting a request before the
handler runs, and a `[SkipKonduit]` message that is never instrumented.

## Konduit.Http.Sample

A typed `HttpClient` service wrapped with `AddMiddleware<T>()`, against a fake backend so it needs no
network.

The interesting part is the contrast with a `DelegatingHandler`, which runs in the same pipeline:

- the **retry** re-runs the whole method, so you see two HTTP calls
- the **cache** short-circuits the call completely, so no HTTP happens at all — something a handler
  cannot do, since by then the request already exists
- the **handler** adds a correlation header underneath both, on every attempt

It also shows `[SkipKonduit]` on the *implementation*, because the client interface is the kind you
would get from a generated contract and cannot annotate, and a **named** client behaving exactly
like an unnamed one.
