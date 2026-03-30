using System.IO;
using OptiBat.Models;
using OptiBat.Services;

namespace OptiBat.Tests;

public sealed class SnapshotStoreTests : IDisposable
{
    private readonly string _tempFile;

    public SnapshotStoreTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    [Fact]
    public void Store_And_Get_Returns_Snapshot()
    {
        var store = new SnapshotStore(_tempFile);
        var snapshot = new DomainSnapshot { DomainId = "test-domain" };
        snapshot.Set("key1", "value1");
        snapshot.Set("key2", 42);

        store.Store(snapshot);
        var result = store.Get("test-domain");

        Assert.NotNull(result);
        Assert.Equal("test-domain", result.DomainId);
        Assert.Equal("value1", result.Get<string>("key1"));
        Assert.Equal(42, result.Get<int>("key2"));
    }

    [Fact]
    public void Get_NonExistent_Returns_Null()
    {
        var store = new SnapshotStore(_tempFile);
        Assert.Null(store.Get("nonexistent"));
    }

    [Fact]
    public void Remove_Deletes_Snapshot()
    {
        var store = new SnapshotStore(_tempFile);
        var snapshot = new DomainSnapshot { DomainId = "to-remove" };
        store.Store(snapshot);

        store.Remove("to-remove");

        Assert.Null(store.Get("to-remove"));
    }

    [Fact]
    public void Clear_Removes_All()
    {
        var store = new SnapshotStore(_tempFile);
        store.Store(new DomainSnapshot { DomainId = "a" });
        store.Store(new DomainSnapshot { DomainId = "b" });

        store.Clear();

        Assert.False(store.HasSnapshots);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Persists_To_Disk_And_Reloads()
    {
        var store1 = new SnapshotStore(_tempFile);
        var snapshot = new DomainSnapshot { DomainId = "persist-test" };
        snapshot.Set("value", 99);
        store1.Store(snapshot);

        // Create new store from same file — should reload
        var store2 = new SnapshotStore(_tempFile);
        var result = store2.Get("persist-test");

        Assert.NotNull(result);
        Assert.Equal(99, result.Get<int>("value"));
    }

    [Fact]
    public void HasSnapshots_Returns_Correctly()
    {
        var store = new SnapshotStore(_tempFile);
        Assert.False(store.HasSnapshots);

        store.Store(new DomainSnapshot { DomainId = "x" });
        Assert.True(store.HasSnapshots);
    }

    [Fact]
    public void Handles_Corrupted_File_Gracefully()
    {
        File.WriteAllText(_tempFile, "not valid json{{{");
        var store = new SnapshotStore(_tempFile);

        Assert.False(store.HasSnapshots);
        Assert.Null(store.Get("anything"));
    }

    public void Dispose()
    {
        try { File.Delete(_tempFile); } catch { }
    }
}
