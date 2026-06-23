using Datateal.Core.Mediator;
using Datateal.Core.Nodes;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetNodesRequest : IRequest<IReadOnlyList<NodeInfo>>;

internal class GetNodesHandler(INodeRepository nodeRepository) : IRequestHandler<GetNodesRequest, IReadOnlyList<NodeInfo>>
{
    public Task<IReadOnlyList<NodeInfo>> Handle(GetNodesRequest request, CancellationToken cancellationToken) =>
        nodeRepository.GetNodesAsync(cancellationToken);
}
