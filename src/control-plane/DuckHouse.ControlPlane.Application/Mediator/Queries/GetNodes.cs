using DuckHouse.Core.Mediator;
using DuckHouse.Core.Nodes;
using DuckHouse.ControlPlane.Core.Repositories;
using DuckHouse.ControlPlane.Core.Services;

namespace DuckHouse.ControlPlane.Application.Mediator.Queries;

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
