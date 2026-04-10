using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetNodePoolConfigRequest(Guid Id) : IRequest<NodePoolConfig?>;

internal class GetNodePoolConfigHandler(INodePoolConfigRepository repository)
    : IRequestHandler<GetNodePoolConfigRequest, NodePoolConfig?>
{
    public async Task<NodePoolConfig?> Handle(GetNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        return await repository.GetAsync(request.Id, cancellationToken);
    }
}
