using Datateal.Core.Deployment;
using Datateal.Data;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

/// <summary>
/// Applies a <see cref="DeploymentStatus"/> transition to a persisted <see cref="DeploymentLog"/>
/// and saves it. Shared by <see cref="WorkspaceDeploymentService"/> and
/// <see cref="AdminDeploymentService"/> so the status-transition switch isn't duplicated between
/// the two deployment scopes.
/// </summary>
internal static class DeploymentLogStatusUpdater
{
    public static async Task UpdateStatusAsync(
        DatatealDbContext db,
        Guid logId,
        DeploymentStatus status,
        string? failureReason,
        CancellationToken ct)
    {
        var log = await db.DeploymentLogs.FirstOrDefaultAsync(l => l.Id == logId, ct)
            ?? throw new InvalidOperationException($"Deployment log '{logId}' was not found.");

        switch (status)
        {
            case DeploymentStatus.ApplyingUi:
                log.TransitionToApplyingUi();
                break;
            case DeploymentStatus.ApplyingJobs:
                log.TransitionToApplyingJobs();
                break;
            case DeploymentStatus.Completed:
                log.TransitionToCompleted();
                break;
            case DeploymentStatus.RollingBack:
                log.TransitionToRollingBack(failureReason ?? "Rollback initiated.");
                break;
            case DeploymentStatus.RolledBack:
                log.TransitionToRolledBack();
                break;
            case DeploymentStatus.Failed:
                log.TransitionToFailed(failureReason ?? "Deployment operation failed.");
                break;
        }

        await db.SaveChangesAsync(ct);
    }
}
