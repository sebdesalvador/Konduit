using Konduit;

namespace Konduit.Tests.Fakes;

internal interface IGreeter
{
    string Greet(string name);

    Task<string> GreetAsync(string name, CancellationToken cancellationToken);

    void Record(string name);

    [SkipKonduit]
    string GreetDirectly(string name);
}

internal sealed class Greeter : IGreeter
{
    public List<string> SeenNames { get; } = [];

    public string Greet(string name)
    {
        SeenNames.Add(name);
        return $"Hello, {name}";
    }

    public async Task<string> GreetAsync(string name, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        SeenNames.Add(name);
        return $"Hello, {name}";
    }

    public void Record(string name) => SeenNames.Add(name);

    public string GreetDirectly(string name)
    {
        SeenNames.Add(name);
        return $"Hello, {name}";
    }
}

internal sealed class EmptyServiceProvider : IServiceProvider
{
    public static readonly EmptyServiceProvider Instance = new();

    public object? GetService(Type serviceType) => null;
}
