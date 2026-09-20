using Konduit.MediatR.Sample;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection().AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

// MediatR scans for handlers, so there is no registration to chain WithMiddleware onto.
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetOrder).Assembly));

// One call wraps every handler it found.
var konduit = services.AddKonduitToMediatRHandlers()
    .WithMiddleware<LoggingMiddleware>()
    .WithMiddleware<ValidationMiddleware>();

Console.WriteLine($"Wrapped {konduit.HandlerCount} handlers.");
Console.WriteLine();

var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

Console.WriteLine("A query — middleware sees the awaited response:");
Console.WriteLine($"  result: {await mediator.Send(new GetOrder(7))}");

Console.WriteLine();
Console.WriteLine("A command returning nothing — Result stays null:");
await mediator.Send(new CancelOrder(7));

Console.WriteLine();
Console.WriteLine("An event with two subscribers — the pipeline runs once around each:");
await mediator.Publish(new OrderPlaced(7));

Console.WriteLine();
Console.WriteLine("Validation short-circuits before the handler:");
try
{
    await mediator.Send(new GetOrder(-1));
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"  rejected: {ex.Message.Split(" (")[0]}");
}

Console.WriteLine();
Console.WriteLine("A [SkipKonduit] message runs no middleware at all:");
Console.WriteLine($"  result: {await mediator.Send(new Heartbeat())}");
