using Datateal.ControlPlane.Core.Repositories;
using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Mediator;
using Datateal.Core.Nodes;

namespace Datateal.ControlPlane.Application.Mediator.Queries;

public record GetNodeRequest(string Name) : IRequest<NodeInfo?>;

internal class GetNodeHandler(
    INodeService nodeService,
    INodeConfigRepository nodeConfigRepository) : IRequestHandler<GetNodeRequest, NodeInfo?>
{
    public async Task<NodeInfo?> Handle(GetNodeRequest request, CancellationToken cancellationToken)
    {
        var node = await nodeService.GetNodeAsync(request.Name, cancellationToken);
        if (node is null) return null;

        var config = await nodeConfigRepository.GetAsync(request.Name, cancellationToken);
        return node with
        {
            KernelIdleTimeout = config?.KernelIdleTimeout,
            NodeIdleTimeout = config?.NodeIdleTimeout,
        };
    }
}
