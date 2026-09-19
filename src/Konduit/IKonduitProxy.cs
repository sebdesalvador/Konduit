namespace Konduit;

/// <summary>
/// Implemented by every generated proxy, so callers can reach the implementation it wraps.
/// </summary>
public interface IKonduitProxy
{
    /// <summary>Gets the service implementation this proxy wraps.</summary>
    object KonduitTarget { get; }
}
