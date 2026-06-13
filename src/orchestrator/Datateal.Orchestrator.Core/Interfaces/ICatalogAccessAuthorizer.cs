namespace Datateal.Orchestrator.Core.Interfaces;

/// <summary>
/// Authorizes catalog access for a job's effective owner at run time. Backed by the shared
/// catalog-access resolver so the orchestrator enforces the same user + workspace rules as the
/// interactive UI path.
/// </summary>
public interface ICatalogAccessAuthorizer
{
    /// <summary>
    /// Returns the subset of <paramref name="catalogNames"/> that <paramref name="ownerUserId"/> may
    /// <b>not</b> access in <paramref name="workspaceId"/>. An empty result means all are allowed.
    /// </summary>
    Task<IReadOnlyList<string>> GetInaccessibleAsync(
        Guid ownerUserId, Guid workspaceId, IReadOnlyList<string> catalogNames, CancellationToken ct = default);
}
