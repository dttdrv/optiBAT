using System.IO;
using System.Text.Json;
using OptiBat.Models;

namespace OptiBat.Services;

/// <summary>
/// Persists domain snapshots to disk for crash recovery.
/// If the app dies while on battery, the next launch can still revert.
/// </summary>
public sealed class SnapshotStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, DomainSnapshot> _snapshots = [];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SnapshotStore(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public bool HasSnapshots => _snapshots.Count > 0;

    public DomainSnapshot? Get(string domainId)
    {
        lock (_lock)
        {
            return _snapshots.TryGetValue(domainId, out var snapshot) ? snapshot : null;
        }
    }

    public void Store(DomainSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshots[snapshot.DomainId] = snapshot;
            Persist();
        }
    }

    public void Remove(string domainId)
    {
        lock (_lock)
        {
            _snapshots.Remove(domainId);
            Persist();
        }
    }

    /// <summary>
    /// Remove multiple domains and persist once (used by crash recovery).
    /// </summary>
    public void RemoveRange(IEnumerable<string> domainIds)
    {
        lock (_lock)
        {
            foreach (var id in domainIds)
                _snapshots.Remove(id);
            Persist();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _snapshots.Clear();
            Persist();
        }
    }

    public IReadOnlyCollection<DomainSnapshot> GetAll()
    {
        lock (_lock)
        {
            return _snapshots.Values.ToList().AsReadOnly();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            _snapshots = JsonSerializer.Deserialize<Dictionary<string, DomainSnapshot>>(json, JsonOptions) ?? [];
        }
        catch
        {
            _snapshots = [];
        }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_snapshots, JsonOptions);

            // Atomic write: write to temp file then rename over real file.
            // Prevents corruption if the process crashes mid-write.
            var tmpPath = _filePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _filePath, overwrite: true);
        }
        catch
        {
            // Non-critical — worst case we can't crash-recover
        }
    }
}
