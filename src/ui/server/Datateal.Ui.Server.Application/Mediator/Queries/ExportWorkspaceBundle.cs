using Datateal.Core.Mediator;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record ExportWorkspaceBundleRequest(Guid WorkspaceId) : IRequest<byte[]>;

internal sealed class ExportWorkspaceBundleHandler(
    IWorkspaceDeploymentService deploymentService,
    IHttpClientFactory httpClientFactory) : IRequestHandler<ExportWorkspaceBundleRequest, byte[]>
{
    public async Task<byte[]> Handle(ExportWorkspaceBundleRequest request, CancellationToken cancellationToken)
    {
        var bundle = await deploymentService.ExportAsync(request.WorkspaceId, cancellationToken);
        bundle.Jobs.Clear();
        bundle.Jobs.AddRange(await OrchestratorDeploymentClient.ExportJobsAsync(
            httpClientFactory,
            request.WorkspaceId,
            cancellationToken));

        return BundleWriter.WriteZip(bundle);
    }
}
