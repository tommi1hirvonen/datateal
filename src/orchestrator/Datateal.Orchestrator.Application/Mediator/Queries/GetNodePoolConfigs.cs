using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetNodePoolConfigsRequest : IRequest<IReadOnlyList<NodePoolConfig>>;

internal class GetNodePoolConfigsHandler(INodePoolConfigRepository repository, IWorkspaceContext workspace)
    : IRequestHandler<GetNodePoolConfigsRequest, IReadOnlyList<NodePoolConfig>>
{
    public async Task<IReadOnlyList<NodePoolConfig>> Handle(GetNodePoolConfigsRequest request, CancellationToken cancellationToken)
    {
        var configs = await repository.GetAllAsync(cancellationToken);
        var workspaceId = workspace.CurrentWorkspaceId;
        return workspaceId is null ? configs : configs.Where(c => c.WorkspaceId == workspaceId).ToList();
    }
}
