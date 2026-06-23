using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Queries;

public record GetKernelRequest(string NodeName, string KernelId) : IRequest<KernelInfo>;

internal class GetKernelHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<GetKernelRequest, KernelInfo>
{
    public Task<KernelInfo> Handle(GetKernelRequest request, CancellationToken cancellationToken) =>
        runtimeClient.GetKernelAsync(request.NodeName, request.KernelId, cancellationToken);
}
