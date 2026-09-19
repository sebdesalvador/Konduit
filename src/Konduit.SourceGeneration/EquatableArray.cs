using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Konduit.SourceGeneration;

/// <summary>
/// An immutable array with structural equality, so generator models cache correctly between runs.
/// </summary>
/// <remarks>
/// <see cref="ImmutableArray{T}"/> compares by reference, which would defeat the incremental
/// generator's caching and regenerate every proxy on every keystroke.
/// </remarks>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items = items;

    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    public int Count => _items.IsDefault ? 0 : _items.Length;

    public T this[int index] => _items[index];

    public static EquatableArray<T> From(IEnumerable<T> items) => new(ImmutableArray.CreateRange(items));

    public bool Equals(EquatableArray<T> other)
    {
        if (Count != other.Count)
        {
            return false;
        }

        for (var i = 0; i < Count; i++)
        {
            if (!_items[i].Equals(other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        for (var i = 0; i < Count; i++)
        {
            hash = (hash * 31) + _items[i].GetHashCode();
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return _items[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
