using System.Text.Json;
using Datateal.Core.Deployment;
using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record ApplyAdminDeploymentRequest(Bundle Bundle, string? ActingUserId, IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class ApplyAdminDeploymentHandler(
    IAdminDeploymentService deploymentService,
    IUserRepository userRepository,
    IDeploymentLockManager lockManager) : IRequestHandler<ApplyAdminDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(ApplyAdminDeploymentRequest request, CancellationToken cancellationToken)
    {
        // Admin deployments are tenant-wide (not workspace-scoped), so a single fixed key serializes
        // all of them rather than locking per workspace.
        using var deploymentLock = lockManager.AcquireLock(DeploymentLockKeys.Admin, "the tenant admin scope");

        // Unlike workspace deployments, an admin apply is a single atomic database transaction with
        // no separate external system to coordinate — there is nothing to roll back if it fails
        // partway (the transaction is simply never committed). This log therefore exists purely as
        // an audit trail (who applied what, and when), not as a recoverable saga.
        var preApplySnapshot = await deploymentService.ExportAsync(cancellationToken);

        string? issuedByDisplayName = request.ActingUserId;
        if (!string.IsNullOrWhiteSpace(request.ActingUserId) && Guid.TryParse(request.ActingUserId, out var actingUserGuid))
        {
            var user = await userRepository.GetByIdAsync(actingUserGuid, cancellationToken);
            if (user is not null)
            {
                issuedByDisplayName = $"{user.DisplayName} ({user.Email})";
            }
        }

        var targetBundleJson = JsonSerializer.Serialize(RedactForLogging(request.Bundle));
        var snapshotJson = JsonSerializer.Serialize(preApplySnapshot);

        var logId = await deploymentService.CreateDeploymentLogAsync(
            targetBundleJson,
            snapshotJson,
            request.ActingUserId,
            issuedByDisplayName,
            cancellationToken);

        try
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.ApplyingUi, failureReason: null, cancellationToken);
            var changeSet = await deploymentService.ApplyAsync(request.Bundle, request.Env, cancellationToken);
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.Completed, failureReason: null, cancellationToken);
            return changeSet;
        }
        catch (Exception ex)
        {
            await deploymentService.UpdateDeploymentLogStatusAsync(logId, DeploymentStatus.Failed, ex.Message, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Builds a copy of <paramref name="bundle"/> suitable for persisting in the deployment log's
    /// <c>TargetBundleJson</c> column: <see cref="Bundle.Files"/> is dropped (nothing binary in an
    /// admin bundle needs auditing), and any literal <see cref="CatalogModel.CatalogPassword"/> a
    /// bundle author inlined (instead of a <c>${var.X}</c>/<c>${env.X}</c> reference) is replaced
    /// with a fixed placeholder so it never sits in plaintext in the database.
    /// </summary>
    internal static Bundle RedactForLogging(Bundle bundle) => new()
    {
        Manifest = bundle.Manifest,
        Catalogs = bundle.Catalogs
            .Select(catalog => catalog.CatalogPassword is null
                ? catalog
                : new CatalogModel
                {
                    Name = catalog.Name,
                    Type = catalog.Type,
                    AccessibleFromAllWorkspaces = catalog.AccessibleFromAllWorkspaces,
                    WorkspaceAccess = catalog.WorkspaceAccess,
                    DataPath = catalog.DataPath,
                    CatalogHost = catalog.CatalogHost,
                    CatalogDatabase = catalog.CatalogDatabase,
                    CatalogUser = catalog.CatalogUser,
                    CatalogPassword = "<redacted>",
                })
            .ToList(),
        Workspaces = bundle.Workspaces,
        Memberships = bundle.Memberships,
        UserCatalogAccess = bundle.UserCatalogAccess,
        Files = [],
    };
}
