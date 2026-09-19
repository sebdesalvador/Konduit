using System.Collections.Immutable;

namespace Konduit.SourceGeneration.Tests;

public sealed class EquatableArrayTests
{
    [Fact]
    public void ArraysWithTheSameItemsAreEqual()
    {
        var left = EquatableArray<string>.From(["a", "b"]);
        var right = EquatableArray<string>.From(["a", "b"]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left.Equals((object)right));
    }

    [Fact]
    public void ArraysDifferingInItemsOrLengthAreNotEqual()
    {
        var left = EquatableArray<string>.From(["a", "b"]);

        Assert.NotEqual(left, EquatableArray<string>.From(["a", "c"]));
        Assert.NotEqual(left, EquatableArray<string>.From(["a"]));
        Assert.False(left.Equals("not an array"));
    }

    [Fact]
    public void TheDefaultAndEmptyArraysBehaveAsEmpty()
    {
        Assert.Equal(0, default(EquatableArray<string>).Count);
        Assert.Equal(0, EquatableArray<string>.Empty.Count);
        Assert.Equal(EquatableArray<string>.Empty, default(EquatableArray<string>));
        Assert.Empty(default(EquatableArray<string>));
    }

    [Fact]
    public void ItemsAreReadableByIndexAndByEnumeration()
    {
        var array = new EquatableArray<string>(ImmutableArray.Create("a", "b"));

        Assert.Equal("b", array[1]);
        Assert.Equal(["a", "b"], array.ToList());
        Assert.Equal(["a", "b"], array.Cast<string>().ToList());
    }
}
