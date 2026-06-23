using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Commands;

public record DeleteKernelRequest(string NodeName, string KernelId) : IRequest;

internal class DeleteKernelHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<DeleteKernelRequest>
{
    public Task Handle(DeleteKernelRequest request, CancellationToken cancellationToken) =>
        runtimeClient.DeleteKernelAsync(request.NodeName, request.KernelId, cancellationToken);
}
