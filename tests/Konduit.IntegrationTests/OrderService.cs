namespace Konduit.IntegrationTests;

public sealed class OrderService : IOrderService
{
    public List<string> Calls { get; } = [];

    public string LastAction { get; set; } = string.Empty;

    public event EventHandler? Audited;

    public string Describe(int id)
    {
        Calls.Add($"Describe({id})");
        return $"order {id}";
    }

    public void Cancel(int id) => Calls.Add($"Cancel({id})");

    public async Task SubmitAsync(int id, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"SubmitAsync({id})");
    }

    public async Task<string> ConfirmAsync(int id, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"ConfirmAsync({id})");
        return $"confirmed {id}";
    }

    public async ValueTask ArchiveAsync(int id)
    {
        await Task.Yield();
        Calls.Add($"ArchiveAsync({id})");
    }

    public ValueTask<int> CountAsync()
    {
        Calls.Add("CountAsync()");
        return new ValueTask<int>(Calls.Count);
    }

    public string? FindReference(int id) => id == 0 ? null : $"ref-{id}";

    public IReadOnlyList<string>? FindTags(int id) => id == 0 ? null : ["a", "b"];

    public T Read<T>(string key)
        where T : class, new()
    {
        Calls.Add($"Read<{typeof(T).Name}>({key})");
        return new T();
    }

    public string Describe(int id, string suffix) => $"order {id} {suffix}";

    public string DescribeQuietly(int id)
    {
        Calls.Add($"DescribeQuietly({id})");
        return $"order {id}";
    }

    public string this[int index] => $"item {index}";

    public void RaiseAudited() => Audited?.Invoke(this, EventArgs.Empty);
}
