using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record ApplyAdminDeploymentRequest(Bundle Bundle, IReadOnlyDictionary<string, string>? Env = null) : IRequest<ChangeSet>;

internal sealed class ApplyAdminDeploymentHandler(IAdminDeploymentService deploymentService)
    : IRequestHandler<ApplyAdminDeploymentRequest, ChangeSet>
{
    public Task<ChangeSet> Handle(ApplyAdminDeploymentRequest request, CancellationToken cancellationToken) =>
        deploymentService.ApplyAsync(request.Bundle, request.Env, cancellationToken);
}
