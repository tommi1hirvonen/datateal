using Datateal.ControlPlane.Core.Repositories;
using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Mediator;
using Datateal.Core.Nodes;

namespace Datateal.ControlPlane.Application.Mediator.Queries;

public record GetNodesRequest : IRequest<IReadOnlyList<NodeInfo>>;

internal class GetNodesHandler(
    INodeService nodeService,
    INodeConfigRepository nodeConfigRepository) : IRequestHandler<GetNodesRequest, IReadOnlyList<NodeInfo>>
{
    public async Task<IReadOnlyList<NodeInfo>> Handle(GetNodesRequest request, CancellationToken cancellationToken)
    {
        var nodes = await nodeService.ListNodesAsync(cancellationToken);
        var enriched = new List<NodeInfo>(nodes.Count);
        foreach (var node in nodes)
        {
            var config = await nodeConfigRepository.GetAsync(node.Name, cancellationToken);
            enriched.Add(node with
            {
                KernelIdleTimeout = config?.KernelIdleTimeout,
                NodeIdleTimeout = config?.NodeIdleTimeout,
            });
        }
        return enriched;
    }
}
