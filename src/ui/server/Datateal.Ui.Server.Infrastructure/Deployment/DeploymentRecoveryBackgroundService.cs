using System.Text.Json;
using Datateal.Core.Deployment;
using Datateal.Data;
using Datateal.Ui.Server.Application;
using Datateal.Ui.Server.Core.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

/// <summary>
/// Hosted background service that checks PostgreSQL for uncompleted or interrupted deployment sagas upon UI Server startup
/// (e.g. following a server crash, restart, or eviction mid-deployment) and automatically recovers/rolls back workspace state.
/// </summary>
internal sealed class DeploymentRecoveryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IDeploymentLockManager lockManager,
    ILogger<DeploymentRecoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatatealDbContext>();
            var deploymentService = scope.ServiceProvider.GetRequiredService<IWorkspaceDeploymentService>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            var incompleteLogs = await db.DeploymentLogs
                .Where(l => l.Status == DeploymentStatus.Staging
                         || l.Status == DeploymentStatus.ApplyingUi
                         || l.Status == DeploymentStatus.ApplyingJobs
                         || l.Status == DeploymentStatus.RollingBack)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync(stoppingToken);

            if (incompleteLogs.Count == 0)
                return;

            logger.LogWarning(
                "Found {Count} incomplete deployment saga(s) upon startup. Initiating automatic crash recovery rollback...",
                incompleteLogs.Count);

            foreach (var log in incompleteLogs)
            {
                if (log.Scope == DeploymentScope.Admin)
                {
                    await RecoverAdminLogAsync(db, lockManager, logger, log, stoppingToken);
                    continue;
                }

                await RecoverWorkspaceLogAsync(db, deploymentService, httpClientFactory, lockManager, logger, log, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while executing the deployment recovery background service.");
        }
    }

    /// <summary>
    /// Sweeps a stray incomplete admin-scope <see cref="DeploymentLog"/> to <see cref="DeploymentStatus.Failed"/>.
    /// Admin applies are a single atomic database transaction with no separate external system to
    /// coordinate, so a crash mid-apply means the transaction itself was never committed — there is
    /// nothing to restore. This only closes out the log so it doesn't linger forever as "in progress".
    /// </summary>
    internal static async Task RecoverAdminLogAsync(
        DatatealDbContext db,
        IDeploymentLockManager lockManager,
        ILogger<DeploymentRecoveryBackgroundService> logger,
        DeploymentLog log,
        CancellationToken stoppingToken)
    {
        IDisposable deploymentLock;
        try
        {
            deploymentLock = lockManager.AcquireLock(DeploymentLockKeys.Admin, "the tenant admin scope");
        }
        catch (DeploymentConflictException ex)
        {
            logger.LogWarning(
                "Skipping recovery of admin deployment saga {LogId}: {Reason}",
                log.Id,
                ex.Message);
            return;
        }

        using var _ = deploymentLock;
        log.TransitionToFailed(
            "Automatic recovery: server restarted during an admin deployment apply; the underlying " +
            "transaction was never committed, so no changes were made and none need to be rolled back.");
        await db.SaveChangesAsync(stoppingToken);

        logger.LogInformation(
            "Marked admin deployment saga {LogId} as Failed (its transaction was never committed, so nothing needs to be rolled back).",
            log.Id);
    }

    internal static async Task RecoverWorkspaceLogAsync(
        DatatealDbContext db,
        IWorkspaceDeploymentService deploymentService,
        IHttpClientFactory httpClientFactory,
        IDeploymentLockManager lockManager,
        ILogger<DeploymentRecoveryBackgroundService> logger,
        DeploymentLog log,
        CancellationToken stoppingToken)
    {
        var workspaceId = log.WorkspaceId
            ?? throw new InvalidOperationException($"Workspace-scope deployment saga '{log.Id}' has no WorkspaceId.");

        // Guard against racing a manual apply for the same workspace that may already be
        // running (e.g. a client retried right as the server came back up). Skip rather
        // than fail — the log stays in its current status and is retried on the next pass.
        IDisposable deploymentLock;
        try
        {
            deploymentLock = lockManager.AcquireLock(
                DeploymentLockKeys.Workspace(workspaceId),
                $"workspace '{workspaceId}'");
        }
        catch (DeploymentConflictException ex)
        {
            logger.LogWarning(
                "Skipping recovery of deployment saga {LogId} for workspace {WorkspaceId}: {Reason}",
                log.Id,
                workspaceId,
                ex.Message);
            return;
        }

        using var _ = deploymentLock;
        try
        {
            logger.LogInformation(
                "Recovering interrupted deployment saga {LogId} for workspace {WorkspaceId} (Status: {Status})...",
                log.Id,
                workspaceId,
                log.Status);

            // A saga stuck in Staging crashed before any UI/job apply began (the log is
            // persisted and status transitioned to ApplyingUi as two separate steps).
            // No state was ever mutated, so there is nothing to roll back — simply mark
            // the saga as Failed so it doesn't linger forever as "in progress".
            if (log.Status == DeploymentStatus.Staging)
            {
                log.TransitionToFailed("Automatic recovery: deployment never began applying (server restarted before the apply phase started).");
                await db.SaveChangesAsync(stoppingToken);

                logger.LogInformation(
                    "Marked deployment saga {LogId} for workspace {WorkspaceId} as Failed (no changes had been applied yet).",
                    log.Id,
                    workspaceId);

                return;
            }

            var fullSnapshot = JsonSerializer.Deserialize<WorkspaceDeploymentFullSnapshot>(log.SnapshotJson)
                ?? throw new InvalidOperationException($"Snapshot for deployment saga '{log.Id}' could not be deserialized.");

            if (log.Status != DeploymentStatus.RollingBack)
                log.TransitionToRollingBack("Automatic recovery triggered upon server restart following interrupted deployment.");

            await db.SaveChangesAsync(stoppingToken);

            // Restore UI Database State
            await deploymentService.RestoreSnapshotAsync(workspaceId, fullSnapshot.UiSnapshot, stoppingToken);

            // Restore Orchestrator Jobs if applicable
            if (fullSnapshot.PreviousJobs is not null)
            {
                await OrchestratorDeploymentClient.ApplyJobsAsync(
                    httpClientFactory,
                    workspaceId,
                    fullSnapshot.PreviousJobs,
                    actingUserId: null,
                    stoppingToken);
            }

            log.TransitionToRolledBack();
            await db.SaveChangesAsync(stoppingToken);

            logger.LogInformation(
                "Successfully recovered and rolled back deployment saga {LogId} for workspace {WorkspaceId}.",
                log.Id,
                workspaceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to recover interrupted deployment saga {LogId} for workspace {WorkspaceId}. Marking status as Failed.",
                log.Id,
                workspaceId);

            log.TransitionToFailed($"Startup recovery failed: {ex.Message}");
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
