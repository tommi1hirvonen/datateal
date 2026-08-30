namespace Datateal.Ui.Server.Core.Deployment;

/// <summary>
/// Serializes deployment applies against the same target using non-blocking, fail-fast locks.
/// A second concurrent apply attempt for a target that is already locked is rejected immediately
/// with <see cref="Datateal.Core.Deployment.DeploymentConflictException"/> rather than being queued —
/// queuing would silently delay the caller with no feedback, whereas the deployment saga (snapshot,
/// UI apply, orchestrator job apply, rollback) needs a single owner at a time to avoid interleaving.
/// This is a single-process, in-memory guard: the UI Server is not horizontally scaled, so a
/// process-local lock is sufficient (it would need to become a distributed lock, e.g. a Postgres
/// advisory lock, if that ever changes).
/// </summary>
public interface IDeploymentLockManager
{
    /// <summary>
    /// Attempts to acquire the exclusive lock identified by <paramref name="key"/>. Throws
    /// <see cref="Datateal.Core.Deployment.DeploymentConflictException"/> immediately if another
    /// deployment already holds it. Dispose the returned handle to release the lock.
    /// </summary>
    /// <param name="key">Stable identifier for the lock target — see <see cref="DeploymentLockKeys"/>.</param>
    /// <param name="displayName">Human-readable description of the target, used in the conflict message.</param>
    IDisposable AcquireLock(string key, string displayName);
}

/// <summary>Well-known deployment lock keys so callers never hand-roll key strings.</summary>
public static class DeploymentLockKeys
{
    /// <summary>Lock key for admin-scope deployments, which are tenant-wide (not workspace-scoped).</summary>
    public const string Admin = "admin";

    /// <summary>Lock key for workspace-scope deployments targeting <paramref name="workspaceId"/>.</summary>
    public static string Workspace(Guid workspaceId) => $"workspace:{workspaceId:D}";
}
