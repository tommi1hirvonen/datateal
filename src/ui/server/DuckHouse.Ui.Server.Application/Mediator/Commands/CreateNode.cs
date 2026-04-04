using DuckHouse.Core.Nodes;
using DuckHouse.Ui.Application.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record CreateNodeRequest(string Name, string VmSize) : IRequest<NodeInfo>;

internal class CreateNodeHandler(INodeRepository nodeRepository) : IRequestHandler<CreateNodeRequest, NodeInfo>
{
    private readonly INodeRepository _nodeRepository = nodeRepository;

    public async Task<NodeInfo> Handle(CreateNodeRequest request, CancellationToken cancellationToken)
    {
        var node = await _nodeRepository.CreateNodeAsync(request.Name, request.VmSize, cancellationToken);
        return node;
    }
}