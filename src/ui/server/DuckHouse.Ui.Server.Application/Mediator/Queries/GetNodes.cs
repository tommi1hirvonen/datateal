using DuckHouse.Core.Nodes;
using DuckHouse.Ui.Application.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetNodesRequest : IRequest<IReadOnlyList<NodeInfo>>;

internal class GetNodesRequestHandler(INodeRepository nodeRepository)
    : IRequestHandler<GetNodesRequest, IReadOnlyList<NodeInfo>>
{
    private readonly INodeRepository _nodeRepository = nodeRepository;

    public Task<IReadOnlyList<NodeInfo>> Handle(GetNodesRequest request, CancellationToken cancellationToken)
    {
        return _nodeRepository.GetNodesAsync(cancellationToken);
    }
}