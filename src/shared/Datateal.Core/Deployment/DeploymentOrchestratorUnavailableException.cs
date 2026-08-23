namespace Datateal.Core.Deployment;

/// <summary>
/// Thrown when a workspace deployment plan/apply cannot reach the job orchestrator (connectivity
/// failure or timeout) while planning, applying, or exporting jobs. These calls happen before any
/// workspace or job state has been mutated (job-plan preflight, or the pre-apply job export used
/// for rollback snapshotting), so there is nothing to roll back — the caller only needs to be told
/// clearly that the upstream orchestrator dependency is unavailable and that no changes were made,
/// rather than seeing an opaque unhandled failure.
/// </summary>
public sealed class DeploymentOrchestratorUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
