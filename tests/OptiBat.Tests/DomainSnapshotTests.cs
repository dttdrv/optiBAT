using OptiBat.Models;

namespace OptiBat.Tests;

public sealed class DomainSnapshotTests
{
    [Fact]
    public void Set_And_Get_String()
    {
        var snapshot = new DomainSnapshot { DomainId = "test" };
        snapshot.Set("key", "hello");
        Assert.Equal("hello", snapshot.Get<string>("key"));
    }

    [Fact]
    public void Set_And_Get_Int()
    {
        var snapshot = new DomainSnapshot { DomainId = "test" };
        snapshot.Set("count", 42);
        Assert.Equal(42, snapshot.Get<int>("count"));
    }

    [Fact]
    public void Set_And_Get_Bool()
    {
        var snapshot = new DomainSnapshot { DomainId = "test" };
        snapshot.Set("flag", true);
        Assert.True(snapshot.Get<bool>("flag"));
    }

    [Fact]
    public void Set_And_Get_Dictionary()
    {
        var snapshot = new DomainSnapshot { DomainId = "test" };
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        snapshot.Set("map", dict);

        var result = snapshot.Get<Dictionary<string, int>>("map");
        Assert.NotNull(result);
        Assert.Equal(1, result["a"]);
        Assert.Equal(2, result["b"]);
    }

    [Fact]
    public void Get_Missing_Key_Returns_Default()
    {
        var snapshot = new DomainSnapshot { DomainId = "test" };
        Assert.Null(snapshot.Get<string>("missing"));
        Assert.Equal(0, snapshot.Get<int>("missing"));
    }

    [Fact]
    public void Has_Returns_Correctly()
    {
        var snapshot = new DomainSnapshot { DomainId = "test" };
        snapshot.Set("exists", 1);

        Assert.True(snapshot.Has("exists"));
        Assert.False(snapshot.Has("nope"));
    }
}
