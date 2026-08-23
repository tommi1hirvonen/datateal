using System.Text.Json;
using Datateal.Core.Deployment;
using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record ApplyWorkspaceDeploymentRequest(
    Guid WorkspaceId,
    Bundle Bundle,
    string? ActingUserId,
    WorkspaceDeploymentGrants Grants,
    IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class ApplyWorkspaceDeploymentHandler(
    IWorkspaceDeploymentService deploymentService,
    IHttpClientFactory httpClientFactory,
    IUserRepository userRepository,
    IDeploymentLockManager lockManager) : IRequestHandler<ApplyWorkspaceDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(ApplyWorkspaceDeploymentRequest request, CancellationToken cancellationToken)
    {
        // Held for the entire saga (pre-flight, snapshot, UI apply, job apply, and any rollback) so
        // a second concurrent apply for the same workspace can never interleave with this one.
        using var deploymentLock = lockManager.AcquireLock(
            DeploymentLockKeys.Workspace(request.WorkspaceId),
            $"workspace '{request.WorkspaceId}'");

        // 1. Pre-flight Validation & Dry-Run Phase
        var preflightChanges = await deploymentService.PlanAsync(request.WorkspaceId, request.Bundle, request.Env, cancellationToken);

        ChangeSet? preflightJobChanges = null;
        if (request.Bundle.Jobs.Count > 0)
        {
            preflightJobChanges = await OrchestratorDeploymentClient.PlanJobsAsync(
                httpClientFactory,
                request.WorkspaceId,
                request.Bundle.Jobs,
                request.ActingUserId,
                cancellationToken);
        }

        // Enforce per-resource-type authorization before anything is persisted or mutated: the
        // caller's baseline WorkspaceManage grant (checked by the controller) is not sufficient on
        // its own if this deployment would also touch node pools, environment/secrets/wheels, or
        // jobs — those require their own dedicated policies, same as the direct CRUD endpoints.
        DeploymentAuthorizationEvaluator.EnsureAuthorized(preflightChanges, preflightJobChanges, request.Grants);

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
        // Persist a redacted echo of the uploaded bundle: the raw Files payload (notebook/query
        // source text, wheel binaries) would otherwise be duplicated into this audit row on every
        // apply with no bound on growth, and a bundle author could accidentally inline a literal
        // secret value (instead of a ${var.X}/${env.X} reference) that would then sit in plaintext
        // in the database forever. Neither is needed for audit/troubleshooting purposes — the
        // pre-deployment snapshot (which does need Files, for rollback) already retains the actual
        // prior state, and its secrets are never captured with plaintext values either.
        var targetBundleJson = JsonSerializer.Serialize(RedactForLogging(request.Bundle));
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

    /// <summary>
    /// Builds a copy of <paramref name="bundle"/> suitable for persisting in the deployment log's
    /// <c>TargetBundleJson</c> column: <see cref="Bundle.Files"/> (notebook/query source text,
    /// wheel binaries) is dropped, and any literal <see cref="SecretModel.Value"/> a bundle author
    /// inlined (instead of a <c>${var.X}</c>/<c>${env.X}</c> reference) is replaced with a fixed
    /// placeholder. The original <paramref name="bundle"/> is left untouched for the actual apply.
    /// </summary>
    internal static Bundle RedactForLogging(Bundle bundle) => new()
    {
        Manifest = bundle.Manifest,
        Catalogs = bundle.Catalogs,
        Workspaces = bundle.Workspaces,
        Memberships = bundle.Memberships,
        UserCatalogAccess = bundle.UserCatalogAccess,
        Folders = bundle.Folders,
        Notebooks = bundle.Notebooks,
        Queries = bundle.Queries,
        NodePools = bundle.NodePools,
        EnvironmentVariables = bundle.EnvironmentVariables,
        Secrets = bundle.Secrets
            .Select(secret => new SecretModel
            {
                Key = secret.Key,
                Description = secret.Description,
                Value = secret.Value is null ? null : "<redacted>",
            })
            .ToList(),
        WheelPackages = bundle.WheelPackages,
        Jobs = bundle.Jobs,
        Files = [],
    };
}
