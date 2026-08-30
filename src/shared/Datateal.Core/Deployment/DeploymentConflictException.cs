namespace Datateal.Core.Deployment;

/// <summary>
/// Thrown when a deployment apply is attempted while another deployment apply is already in
/// progress for the same target (a workspace, or the tenant-wide admin scope). Deployment applies
/// span multiple database transactions and, for workspace scope, an external orchestrator call, so
/// letting two applies to the same target run concurrently risks interleaved saga steps (e.g. one
/// apply's rollback reverting another apply's successful changes). Callers must surface this as a
/// distinct, actionable error rather than a generic failure so the user understands they hit a race
/// condition and simply needs to retry once the other deployment finishes.
/// </summary>
public sealed class DeploymentConflictException(string message) : Exception(message);
