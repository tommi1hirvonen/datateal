using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record PlanWorkspaceDeploymentRequest(Guid WorkspaceId, Bundle Bundle, string? ActingUserId, IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class PlanWorkspaceDeploymentHandler(
    IWorkspaceDeploymentService deploymentService,
    IHttpClientFactory httpClientFactory) : IRequestHandler<PlanWorkspaceDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(PlanWorkspaceDeploymentRequest request, CancellationToken cancellationToken)
    {
        var workspaceChanges = await deploymentService.PlanAsync(request.WorkspaceId, request.Bundle, request.Env, cancellationToken);
        if (request.Bundle.Jobs.Count == 0)
            return workspaceChanges;

        var jobChanges = await OrchestratorDeploymentClient.PlanJobsAsync(
            httpClientFactory,
            request.WorkspaceId,
            request.Bundle.Jobs,
            request.ActingUserId,
            cancellationToken);

        return DeploymentChangeSetMerger.Merge(workspaceChanges, jobChanges);
    }
}
