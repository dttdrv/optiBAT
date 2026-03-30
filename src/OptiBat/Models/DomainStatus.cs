namespace OptiBat.Models;

/// <summary>
/// Live status of an optimization domain, bound to the UI.
/// </summary>
public sealed class DomainStatus
{
    public string DomainId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsSupported { get; init; }
    public bool IsActive { get; init; }
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Extra detail lines for expanded view.
    /// </summary>
    public string[] Details { get; init; } = [];
}
