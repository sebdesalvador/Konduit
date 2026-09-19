using Konduit;

namespace Konduit.IntegrationTests;

/// <summary>A base interface, to prove inherited members are proxied too.</summary>
public interface IAudited
{
    string LastAction { get; set; }

    event EventHandler? Audited;
}

/// <summary>Exercises every method shape the generator has to emit.</summary>
public interface IOrderService : IAudited
{
    string Describe(int id);

    void Cancel(int id);

    Task SubmitAsync(int id, CancellationToken cancellationToken);

    Task<string> ConfirmAsync(int id, CancellationToken cancellationToken);

    ValueTask ArchiveAsync(int id);

    ValueTask<int> CountAsync();

    string? FindReference(int id);

    IReadOnlyList<string>? FindTags(int id);

    T Read<T>(string key)
        where T : class, new();

    string Describe(int id, string suffix);

    [SkipKonduit]
    string DescribeQuietly(int id);

    string this[int index] { get; }
}
