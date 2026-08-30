namespace Datateal.Core.Deployment;

/// <summary>
/// Execution status of a deployment saga.
/// </summary>
public enum DeploymentStatus
{
    Staging = 0,
    ApplyingUi = 1,
    ApplyingJobs = 2,
    Completed = 3,
    RollingBack = 4,
    RolledBack = 5,
    Failed = 6,
}
