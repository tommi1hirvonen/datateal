using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record ApplyAdminDeploymentRequest(Bundle Bundle, IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class ApplyAdminDeploymentHandler(
    IAdminDeploymentService deploymentService,
    IDeploymentLockManager lockManager) : IRequestHandler<ApplyAdminDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(ApplyAdminDeploymentRequest request, CancellationToken cancellationToken)
    {
        // Admin deployments are tenant-wide (not workspace-scoped), so a single fixed key serializes
        // all of them rather than locking per workspace.
        using var deploymentLock = lockManager.AcquireLock(DeploymentLockKeys.Admin, "the tenant admin scope");
        return await deploymentService.ApplyAsync(request.Bundle, request.Env, cancellationToken);
    }
}
