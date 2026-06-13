using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record DeleteNodePoolConfigRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteNodePoolConfigHandler(INodePoolConfigRepository repository)
    : IRequestHandler<DeleteNodePoolConfigRequest, bool>
{
    public async Task<bool> Handle(DeleteNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        var config = await repository.GetAsync(request.Id, cancellationToken);
        if (config is null || config.WorkspaceId != request.WorkspaceId) return false;

        return await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
