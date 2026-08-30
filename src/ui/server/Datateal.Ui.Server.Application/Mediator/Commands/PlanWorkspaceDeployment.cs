using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record PlanWorkspaceDeploymentRequest(
    Guid WorkspaceId,
    Bundle Bundle,
    string? ActingUserId,
    WorkspaceDeploymentGrants Grants,
    IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class PlanWorkspaceDeploymentHandler(
    IWorkspaceDeploymentService deploymentService,
    IHttpClientFactory httpClientFactory) : IRequestHandler<PlanWorkspaceDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(PlanWorkspaceDeploymentRequest request, CancellationToken cancellationToken)
    {
        var workspaceChanges = await deploymentService.PlanAsync(request.WorkspaceId, request.Bundle, request.Env, cancellationToken);

        ChangeSet? jobChanges = null;
        if (request.Bundle.Jobs.Count > 0)
        {
            jobChanges = await OrchestratorDeploymentClient.PlanJobsAsync(
                httpClientFactory,
                request.WorkspaceId,
                request.Bundle.Jobs,
                request.ActingUserId,
                cancellationToken);
        }

        DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges, request.Grants);

        return jobChanges is null ? workspaceChanges : DeploymentChangeSetMerger.Merge(workspaceChanges, jobChanges);
    }
}
