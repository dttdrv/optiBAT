namespace OptiBat.Models;

/// <summary>
/// Result of applying a single optimization domain.
/// </summary>
public sealed class ApplyResult
{
    public bool Success { get; init; }
    public string DomainId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int ItemsOptimized { get; init; }
    public int ItemsFailed { get; init; }
    public int ItemsSkipped { get; init; }
    public TimeSpan Duration { get; init; }

    public static ApplyResult Ok(string domainId, string message, int optimized = 0, int failed = 0, int skipped = 0, TimeSpan duration = default)
        => new() { Success = true, DomainId = domainId, Message = message, ItemsOptimized = optimized, ItemsFailed = failed, ItemsSkipped = skipped, Duration = duration };

    public static ApplyResult Fail(string domainId, string message, TimeSpan duration = default)
        => new() { Success = false, DomainId = domainId, Message = message, Duration = duration };
}
