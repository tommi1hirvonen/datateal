namespace Datateal.Core.Deployment;

/// <summary>
/// Domain entity representing a persisted deployment saga log.
/// Encapsulates state transitions so illegal transitions are unrepresentable.
/// </summary>
public class DeploymentLog
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public DeploymentScope Scope { get; private set; }
    public DeploymentStatus Status { get; private set; }
    public string TargetBundleJson { get; private set; } = string.Empty;
    public string SnapshotJson { get; private set; } = string.Empty;
    public string? IssuedByUserId { get; private set; }
    public string? IssuedByDisplayName { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Parameterless constructor for EF Core
    private DeploymentLog() { }

    public static DeploymentLog Create(
        Guid workspaceId,
        DeploymentScope scope,
        string targetBundleJson,
        string snapshotJson,
        string? issuedByUserId = null,
        string? issuedByDisplayName = null)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID cannot be empty.", nameof(workspaceId));
        if (string.IsNullOrWhiteSpace(targetBundleJson))
            throw new ArgumentException("Target bundle JSON cannot be empty.", nameof(targetBundleJson));
        if (string.IsNullOrWhiteSpace(snapshotJson))
            throw new ArgumentException("Snapshot JSON cannot be empty.", nameof(snapshotJson));

        var now = DateTime.UtcNow;
        return new DeploymentLog
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Scope = scope,
            Status = DeploymentStatus.Staging,
            TargetBundleJson = targetBundleJson,
            SnapshotJson = snapshotJson,
            IssuedByUserId = issuedByUserId,
            IssuedByDisplayName = issuedByDisplayName,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void TransitionToApplyingUi()
    {
        EnsureStatus(DeploymentStatus.Staging, DeploymentStatus.ApplyingUi);
        Status = DeploymentStatus.ApplyingUi;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToApplyingJobs()
    {
        EnsureStatus(DeploymentStatus.ApplyingUi, DeploymentStatus.ApplyingJobs);
        Status = DeploymentStatus.ApplyingJobs;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToCompleted()
    {
        if (Status is not (DeploymentStatus.ApplyingUi or DeploymentStatus.ApplyingJobs))
        {
            throw new InvalidOperationException(
                $"Invalid deployment state transition from '{Status}' to '{DeploymentStatus.Completed}'.");
        }

        Status = DeploymentStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToRollingBack(string reason)
    {
        if (Status is DeploymentStatus.Completed or DeploymentStatus.RolledBack or DeploymentStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Cannot initiate rollback for deployment in status '{Status}'.");
        }

        Status = DeploymentStatus.RollingBack;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToRolledBack()
    {
        EnsureStatus(DeploymentStatus.RollingBack, DeploymentStatus.RolledBack);
        Status = DeploymentStatus.RolledBack;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToFailed(string reason)
    {
        Status = DeploymentStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureStatus(DeploymentStatus expectedCurrent, DeploymentStatus target)
    {
        if (Status != expectedCurrent)
        {
            throw new InvalidOperationException(
                $"Invalid deployment state transition from '{Status}' to '{target}'. Expected current status '{expectedCurrent}'.");
        }
    }
}
