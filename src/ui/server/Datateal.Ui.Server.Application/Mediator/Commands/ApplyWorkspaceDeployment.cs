using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record ApplyWorkspaceDeploymentRequest(Guid WorkspaceId, Bundle Bundle, string? ActingUserId) : IRequest<ChangeSet>;

internal sealed class ApplyWorkspaceDeploymentHandler(
    IWorkspaceDeploymentService deploymentService,
    IHttpClientFactory httpClientFactory) : IRequestHandler<ApplyWorkspaceDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(ApplyWorkspaceDeploymentRequest request, CancellationToken cancellationToken)
    {
        var workspaceChanges = await deploymentService.ApplyAsync(request.WorkspaceId, request.Bundle, cancellationToken);
        if (request.Bundle.Jobs.Count == 0)
            return workspaceChanges;

        var jobChanges = await OrchestratorDeploymentClient.ApplyJobsAsync(
            httpClientFactory,
            request.WorkspaceId,
            request.Bundle.Jobs,
            request.ActingUserId,
            cancellationToken);

        return DeploymentChangeSetMerger.Merge(workspaceChanges, jobChanges);
    }
}
