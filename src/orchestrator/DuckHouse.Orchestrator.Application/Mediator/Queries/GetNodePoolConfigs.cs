using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetNodePoolConfigsRequest : IRequest<IReadOnlyList<NodePoolConfig>>;

internal class GetNodePoolConfigsHandler(INodePoolConfigRepository repository)
    : IRequestHandler<GetNodePoolConfigsRequest, IReadOnlyList<NodePoolConfig>>
{
    public async Task<IReadOnlyList<NodePoolConfig>> Handle(GetNodePoolConfigsRequest request, CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}
