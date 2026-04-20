namespace DuckHouse.Orchestrator.Application.Validation;

/// <summary>
/// Validates node pool configuration names. These are user-facing labels only;
/// actual Kubernetes node names are derived from pool IDs, not from the pool name.
/// </summary>
public static class NodeNameValidator
{
    private const int MaxNodePoolNameLength = 100;

    /// <summary>
    /// Validates a node pool configuration name.
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateNodePoolName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "Node pool name is required.";

        if (name.Length > MaxNodePoolNameLength)
            return $"Node pool name must be {MaxNodePoolNameLength} characters or fewer.";

        return null;
    }
}
