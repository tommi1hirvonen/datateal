using Datateal.Core.Mediator;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record ExportAdminBundleRequest() : IRequest<byte[]>;

internal sealed class ExportAdminBundleHandler(IAdminDeploymentService deploymentService)
    : IRequestHandler<ExportAdminBundleRequest, byte[]>
{
    public async Task<byte[]> Handle(ExportAdminBundleRequest request, CancellationToken cancellationToken) =>
        BundleWriter.WriteZip(await deploymentService.ExportAsync(cancellationToken));
}
