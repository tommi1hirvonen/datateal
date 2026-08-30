using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record PlanAdminDeploymentRequest(Bundle Bundle, IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class PlanAdminDeploymentHandler(IAdminDeploymentService deploymentService)
    : IRequestHandler<PlanAdminDeploymentRequest, ChangeSet>
{
    public Task<ChangeSet> Handle(PlanAdminDeploymentRequest request, CancellationToken cancellationToken) =>
        deploymentService.PlanAsync(request.Bundle, request.Env, cancellationToken);
}
