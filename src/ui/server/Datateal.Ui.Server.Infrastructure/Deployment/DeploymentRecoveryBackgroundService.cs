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
                .Where(l => l.Status == DeploymentStatus.ApplyingUi
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
                // Guard against racing a manual apply for the same workspace that may already be
                // running (e.g. a client retried right as the server came back up). Skip rather
                // than fail — the log stays in its current status and is retried on the next pass.
                IDisposable deploymentLock;
                try
                {
                    deploymentLock = lockManager.AcquireLock(
                        DeploymentLockKeys.Workspace(log.WorkspaceId),
                        $"workspace '{log.WorkspaceId}'");
                }
                catch (DeploymentConflictException ex)
                {
                    logger.LogWarning(
                        "Skipping recovery of deployment saga {LogId} for workspace {WorkspaceId}: {Reason}",
                        log.Id,
                        log.WorkspaceId,
                        ex.Message);
                    continue;
                }

                using var _ = deploymentLock;
                try
                {
                    logger.LogInformation(
                        "Recovering interrupted deployment saga {LogId} for workspace {WorkspaceId} (Status: {Status})...",
                        log.Id,
                        log.WorkspaceId,
                        log.Status);

                    var fullSnapshot = JsonSerializer.Deserialize<WorkspaceDeploymentFullSnapshot>(log.SnapshotJson)
                        ?? throw new InvalidOperationException($"Snapshot for deployment saga '{log.Id}' could not be deserialized.");

                    if (log.Status != DeploymentStatus.RollingBack)
                        log.TransitionToRollingBack("Automatic recovery triggered upon server restart following interrupted deployment.");

                    await db.SaveChangesAsync(stoppingToken);

                    // Restore UI Database State
                    await deploymentService.RestoreSnapshotAsync(log.WorkspaceId, fullSnapshot.UiSnapshot, stoppingToken);

                    // Restore Orchestrator Jobs if applicable
                    if (fullSnapshot.PreviousJobs is not null)
                    {
                        await OrchestratorDeploymentClient.ApplyJobsAsync(
                            httpClientFactory,
                            log.WorkspaceId,
                            fullSnapshot.PreviousJobs,
                            actingUserId: null,
                            stoppingToken);
                    }

                    log.TransitionToRolledBack();
                    await db.SaveChangesAsync(stoppingToken);

                    logger.LogInformation(
                        "Successfully recovered and rolled back deployment saga {LogId} for workspace {WorkspaceId}.",
                        log.Id,
                        log.WorkspaceId);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to recover interrupted deployment saga {LogId} for workspace {WorkspaceId}. Marking status as Failed.",
                        log.Id,
                        log.WorkspaceId);

                    log.TransitionToFailed($"Startup recovery failed: {ex.Message}");
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while executing the deployment recovery background service.");
        }
    }
}
