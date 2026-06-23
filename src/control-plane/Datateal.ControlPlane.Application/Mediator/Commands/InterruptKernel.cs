using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Commands;

public record InterruptKernelRequest(string NodeName, string KernelId) : IRequest;

internal class InterruptKernelHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<InterruptKernelRequest>
{
    public Task Handle(InterruptKernelRequest request, CancellationToken cancellationToken) =>
        runtimeClient.InterruptKernelAsync(request.NodeName, request.KernelId, cancellationToken);
}
