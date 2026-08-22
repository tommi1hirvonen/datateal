using System.Text.Json;
using Datateal.Core.Deployment;
using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record ApplyWorkspaceDeploymentRequest(Guid WorkspaceId, Bundle Bundle, string? ActingUserId, IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class ApplyWorkspaceDeploymentHandler(
    IWorkspaceDeploymentService deploymentService,
    IHttpClientFactory httpClientFactory,
    IUserRepository userRepository) : IRequestHandler<ApplyWorkspaceDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(ApplyWorkspaceDeploymentRequest request, CancellationToken cancellationToken)
    {
        // 1. Pre-flight Validation & Dry-Run Phase
        await deploymentService.PlanAsync(request.WorkspaceId, request.Bundle, request.Env, cancellationToken);

        if (request.Bundle.Jobs.Count > 0)
        {
            await OrchestratorDeploymentClient.PlanJobsAsync(
                httpClientFactory,
                request.WorkspaceId,
                request.Bundle.Jobs,
                request.ActingUserId,
                cancellationToken);
        }

        // 2. Snapshot Pre-Deployment State for Rollback & Persist Saga Log
        var uiSnapshot = await deploymentService.CreateSnapshotAsync(request.WorkspaceId, cancellationToken);
        List<Datateal.Deployment.Models.JobModel>? previousJobs = null;
        if (request.Bundle.Jobs.Count > 0)
        {
            previousJobs = await OrchestratorDeploymentClient.ExportJobsAsync(
                httpClientFactory,
                request.WorkspaceId,
                cancellationToken);
        }

        string? issuedByDisplayName = request.ActingUserId;
        if (!string.IsNullOrWhiteSpace(request.ActingUserId) && Guid.TryParse(request.ActingUserId, out var actingUserGuid))
        {
            var user = await userRepository.GetByIdAsync(actingUserGuid, cancellationToken);
            if (user is not null)
            {
                issuedByDisplayName = $"{user.DisplayName} ({user.Email})";
            }
        }

        var fullSnapshot = new WorkspaceDeploymentFullSnapshot(uiSnapshot, previousJobs);
        var targetBundleJson = JsonSerializer.Serialize(request.Bundle);
        var snapshotJson = JsonSerializer.Serialize(fullSnapshot);

        var logId = await deploymentService.CreateDeploymentLogAsync(
            request.WorkspaceId,
            DeploymentScope.Workspace,
            targetBundleJson,
            snapshotJson,
            request.ActingUserId,
            issuedByDisplayName,
            cancellationToken);

        // 3. Apply UI Database Resources
        ChangeSet workspaceChanges;
        try
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.ApplyingUi, failureReason: null, cancellationToken);
            workspaceChanges = await deploymentService.ApplyAsync(request.WorkspaceId, request.Bundle, request.Env, cancellationToken);
        }
        catch (Exception ex)
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.Failed, $"Pre-apply failed: {ex.Message}", cancellationToken);
            throw new InvalidOperationException($"Workspace deployment pre-apply failed: {ex.Message}", ex);
        }

        if (request.Bundle.Jobs.Count == 0)
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.Completed, failureReason: null, cancellationToken);
            return workspaceChanges;
        }

        // 4. Apply Orchestrator Jobs with Persisted Saga Rollback
        try
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.ApplyingJobs, failureReason: null, cancellationToken);

            var jobChanges = await OrchestratorDeploymentClient.ApplyJobsAsync(
                httpClientFactory,
                request.WorkspaceId,
                request.Bundle.Jobs,
                request.ActingUserId,
                cancellationToken);

            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.Completed, failureReason: null, cancellationToken);
            return DeploymentChangeSetMerger.Merge(workspaceChanges, jobChanges);
        }
        catch (Exception ex)
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.RollingBack, ex.Message, cancellationToken);

            try
            {
                await deploymentService.RestoreSnapshotAsync(request.WorkspaceId, uiSnapshot, cancellationToken);
                if (previousJobs is not null)
                {
                    await OrchestratorDeploymentClient.ApplyJobsAsync(
                        httpClientFactory,
                        request.WorkspaceId,
                        previousJobs,
                        request.ActingUserId,
                        cancellationToken);
                }

                await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.RolledBack, failureReason: null, cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.Failed, $"Rollback failed: {rollbackEx.Message}. Root cause: {ex.Message}", cancellationToken);

                throw new InvalidOperationException(
                    $"Workspace deployment failed during job apply: {ex.Message}. " +
                    $"Additionally, rolling back workspace state failed: {rollbackEx.Message}",
                    ex);
            }

            throw new InvalidOperationException(
                $"Workspace deployment failed and was automatically rolled back to its previous state. Cause: {ex.Message}",
                ex);
        }
    }
}
