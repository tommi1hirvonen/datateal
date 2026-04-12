using DuckHouse.Core.Mediator;
using DuckHouse.Core.Nodes;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record CreateNodeRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout = null,
    TimeSpan? NodeIdleTimeout = null,
    string? KernelRequirements = null,
    IReadOnlyList<WheelContent>? WheelContents = null) : IRequest<NodeInfo>;

internal class CreateNodeHandler(INodeRepository nodeRepository) : IRequestHandler<CreateNodeRequest, NodeInfo>
{
    public Task<NodeInfo> Handle(CreateNodeRequest request, CancellationToken cancellationToken) =>
        nodeRepository.CreateNodeAsync(
            request.Name,
            request.VmSize,
            request.KernelIdleTimeout,
            request.NodeIdleTimeout,
            request.KernelRequirements,
            request.WheelContents,
            cancellationToken);
}