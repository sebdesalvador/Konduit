using Konduit;

namespace Konduit.Sample;

public interface IOrderService
{
    Task<string> PlaceAsync(string item, CancellationToken cancellationToken);

    string Describe(int id);

    [SkipKonduit]
    int Count();
}

public sealed class OrderService : IOrderService
{
    private int _attempts;

    public async Task<string> PlaceAsync(string item, CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);

        // Fails once, to show the retry middleware doing its job.
        if (++_attempts == 1)
        {
            throw new TimeoutException("the warehouse did not answer");
        }

        return $"order for {item}";
    }

    public string Describe(int id) => $"order #{id}";

    public int Count() => _attempts;
}
