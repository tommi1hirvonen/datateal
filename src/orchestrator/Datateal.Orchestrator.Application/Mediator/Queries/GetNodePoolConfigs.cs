using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetNodePoolConfigsRequest(Guid WorkspaceId) : IRequest<IReadOnlyList<NodePoolConfig>>;

internal class GetNodePoolConfigsHandler(INodePoolConfigRepository repository)
    : IRequestHandler<GetNodePoolConfigsRequest, IReadOnlyList<NodePoolConfig>>
{
    public async Task<IReadOnlyList<NodePoolConfig>> Handle(GetNodePoolConfigsRequest request, CancellationToken cancellationToken)
    {
        return await repository.GetByWorkspaceAsync(request.WorkspaceId, cancellationToken);
    }
}
