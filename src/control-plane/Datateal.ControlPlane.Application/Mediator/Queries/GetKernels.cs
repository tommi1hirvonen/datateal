using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Queries;

public record GetKernelsRequest(string NodeName) : IRequest<IReadOnlyList<KernelInfo>>;

internal class GetKernelsHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<GetKernelsRequest, IReadOnlyList<KernelInfo>>
{
    public Task<IReadOnlyList<KernelInfo>> Handle(GetKernelsRequest request, CancellationToken cancellationToken) =>
        runtimeClient.ListKernelsAsync(request.NodeName, cancellationToken);
}
