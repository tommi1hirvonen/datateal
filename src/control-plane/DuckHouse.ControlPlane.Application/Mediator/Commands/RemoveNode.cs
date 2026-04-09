using DuckHouse.Core.Mediator;
using DuckHouse.ControlPlane.Core.Repositories;
using DuckHouse.ControlPlane.Core.Services;

namespace DuckHouse.ControlPlane.Application.Mediator.Commands;

public record RemoveNodeRequest(string Name) : IRequest;

internal class RemoveNodeHandler(
    INodeService nodeService,
    INodeConfigRepository nodeConfigRepository) : IRequestHandler<RemoveNodeRequest>
{
    public async Task Handle(RemoveNodeRequest request, CancellationToken cancellationToken)
    {
        await nodeService.RemoveNodeAsync(request.Name, cancellationToken);
        await nodeConfigRepository.DeleteAsync(request.Name, cancellationToken);
    }
}
