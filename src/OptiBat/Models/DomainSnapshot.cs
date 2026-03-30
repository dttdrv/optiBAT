using System.Text.Json;

namespace OptiBat.Models;

/// <summary>
/// Captures the exact pre-optimization state of a single domain.
/// Persisted to disk for crash recovery.
/// </summary>
public sealed class DomainSnapshot
{
    public string DomainId { get; init; } = string.Empty;
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Domain-specific key-value state. Each domain decides what to store.
    /// Values are JsonElement when deserialized, or primitive types when created.
    /// </summary>
    public Dictionary<string, JsonElement> State { get; init; } = [];

    /// <summary>
    /// Store a typed value into the snapshot.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        var json = JsonSerializer.SerializeToElement(value);
        State[key] = json;
    }

    /// <summary>
    /// Retrieve a typed value from the snapshot.
    /// </summary>
    public T? Get<T>(string key)
    {
        if (!State.TryGetValue(key, out var element))
            return default;
        return element.Deserialize<T>();
    }

    /// <summary>
    /// Check if a key exists in the snapshot.
    /// </summary>
    public bool Has(string key) => State.ContainsKey(key);
}
