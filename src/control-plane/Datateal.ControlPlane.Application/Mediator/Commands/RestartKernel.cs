using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Commands;

public record RestartKernelRequest(string NodeName, string KernelId) : IRequest<KernelInfo>;

internal class RestartKernelHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<RestartKernelRequest, KernelInfo>
{
    public Task<KernelInfo> Handle(RestartKernelRequest request, CancellationToken cancellationToken) =>
        runtimeClient.RestartKernelAsync(request.NodeName, request.KernelId, cancellationToken);
}
